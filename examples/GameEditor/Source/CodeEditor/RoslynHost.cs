// RoslynHost.cs — Lightweight Roslyn-backed code intelligence host.
//
// Provides:
//   • Real-time C# diagnostics via CSharpCompilation (syntax + semantic)
//   • Code-completion entries via CompletionService (Workspaces + Features)
//
// Usage pattern from ScriptEditorWindow:
//   RoslynHost.Instance.UpdateDocument(filePath, sourceText);
//   // after debounce fires → GetDiagnosticsAsync / GetCompletionsAsync

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

using GameEditor.CodeEditor;

namespace GameEditor.CodeEditor
{
    // ── Navigation models ─────────────────────────────────────────────────────

    /// <summary>A resolved source location returned by Roslyn navigation queries.</summary>
    public readonly struct SymbolLocation
    {
        public string FilePath  { get; init; }  // absolute path
        public int    Line      { get; init; }  // 1-based
        public int    Column    { get; init; }  // 1-based
        public string Label     { get; init; }  // display text for list items
    }

    // ── Completion models ────────────────────────────────────────────────────

    public enum CompletionItemKind
    {
        Text, Method, Function, Constructor, Field, Variable, Class, Interface,
        Module, Property, Unit, Value, Enum, Keyword, Snippet, Color, File,
        Reference, Folder, EnumMember, Constant, Struct, Event, Operator, TypeParameter,
        Namespace, Unknown
    }

    public readonly struct CompletionEntry
    {
        public string             Label       { get; init; }
        public string             Detail      { get; init; }  // e.g. "System.String"
        public string             InsertText  { get; init; }
        public CompletionItemKind Kind        { get; init; }
    }

    // ── Signature help model ─────────────────────────────────────────────────

    public class SignatureInfo
    {
        public string ReturnType     { get; init; } = "";
        public string MethodName     { get; init; } = "";
        /// <summary>(Type, Name) pairs for all parameters of the best-matching overload.</summary>
        public IReadOnlyList<(string Type, string Name)> Parameters { get; init; }
            = Array.Empty<(string, string)>();
        public string Summary        { get; init; } = ""; // method XML doc summary
        /// <summary>Documentation for the currently active parameter (falls back to Summary).</summary>
        public string ActiveParamDoc { get; init; } = "";
        public int    ActiveParam    { get; init; }        // 0-based index of highlighted param
        public int    OverloadCount  { get; init; } = 1;  // total overloads found
    }

    // ── RoslynHost ───────────────────────────────────────────────────────────

    /// <summary>
    /// Singleton host that maintains an <see cref="AdhocWorkspace"/> and a single
    /// C# project for the open script files.  Supports concurrent accesses from
    /// the render thread (read) and background tasks (write).
    /// </summary>
    public sealed class RoslynHost : IDisposable
    {
        // ── Singleton ────────────────────────────────────────────────────────

        private static RoslynHost? _instance;
        public  static RoslynHost   Instance => _instance ??= new RoslynHost();

        // ── State ────────────────────────────────────────────────────────────

        private readonly AdhocWorkspace         _workspace;
        private readonly ProjectId              _projectId;
        private readonly object                 _lock = new();

        // Map filePath → documentId (lazily created)
        private readonly Dictionary<string, DocumentId> _docIds
            = new(StringComparer.OrdinalIgnoreCase);

        // In-flight analysis CTS — cancelled each time the source changes
        private CancellationTokenSource _analysisCts = new();

        // Latest diagnostic results per file
        private readonly Dictionary<string, IReadOnlyList<BuildDiagnostic>> _diagnostics
            = new(StringComparer.OrdinalIgnoreCase);

        // ── Events ───────────────────────────────────────────────────────────

        /// <summary>
        /// Raised on a thread-pool thread after diagnostics for <paramref name="filePath"/>
        /// have been refreshed.
        /// </summary>
        public event Action<string, IReadOnlyList<BuildDiagnostic>>? DiagnosticsChanged;

        // ── Construction ─────────────────────────────────────────────────────

        private RoslynHost()
        {
            // MefHostServices.DefaultAssemblies only includes Workspaces assemblies.
            // Completion providers (member-access, keyword, etc.) live in the Features
            // assemblies — without them CompletionService returns empty results.
            // Load them from the same directory as the already-known Workspaces assembly.
            string roslynDir = Path.GetDirectoryName(typeof(AdhocWorkspace).Assembly.Location)!;
            var mefAssemblies = MefHostServices.DefaultAssemblies;
            foreach (string featAsmName in new[] {
                "Microsoft.CodeAnalysis.Features.dll",
                "Microsoft.CodeAnalysis.CSharp.Features.dll" })
            {
                string path = Path.Combine(roslynDir, featAsmName);
                if (File.Exists(path))
                    try { mefAssemblies = mefAssemblies.Add(Assembly.LoadFrom(path)); } catch { }
            }

            var host = MefHostServices.Create(mefAssemblies);
            _workspace  = new AdhocWorkspace(host);
            _projectId  = ProjectId.CreateNewId("GameProject");

            // Load references: prefer the SDK reference pack directory over the shared runtime.
            // Reference packs (packs/Microsoft.NETCore.App.Ref/{ver}/ref/net{major}.{minor}/)
            // contain reference-only assemblies AND sibling XML documentation files, which the
            // shared runtime directory does NOT have. This is what enables xmlDoc descriptions.
            string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            string refDir     = FindNetRefPackDirectory(runtimeDir) ?? runtimeDir;
            Console.Error.WriteLine($"[Roslyn] BCL ref dir: {refDir}");
            var runtimeRefs = Directory
                .GetFiles(refDir, "*.dll", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    string name = Path.GetFileName(f);
                    return !name.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase)
                        && !name.Contains(".resources.", StringComparison.OrdinalIgnoreCase);
                })
                .Select(f => (MetadataReference)MetadataReference.CreateFromFile(f,
                    documentation: XmlDocFromSiblingFile(f)));

            // Also add non-BCL assemblies already loaded in this AppDomain (game framework,
            // Sokol bindings, etc.) that live outside the runtime directory.
            var domainRefs = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a =>
                {
                    if (a.IsDynamic || string.IsNullOrEmpty(a.Location)) return false;
                    string dir = Path.GetDirectoryName(a.Location) ?? "";
                    return !string.Equals(dir, runtimeDir, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(dir, refDir,     StringComparison.OrdinalIgnoreCase);
                })
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location,
                    documentation: XmlDocFromSiblingFile(a.Location)));

            var refs = runtimeRefs.Concat(domainRefs).ToArray();

            var projectInfo = ProjectInfo.Create(
                _projectId,
                VersionStamp.Create(),
                name:               "GameProject",
                assemblyName:       "GameProject",
                language:           LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithNullableContextOptions(NullableContextOptions.Enable),
                parseOptions:       CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                metadataReferences: refs);

            _workspace.AddProject(projectInfo);

            // Inject a synthetic global-usings document that mirrors .NET's
            // implicit usings (SDK-style project with ImplicitUsings=enable).
            // Without this, types like MathF appear unresolved in the AdhocWorkspace
            // even though the actual build succeeds — because the real compiler
            // generates these global usings automatically from the SDK.
            const string globalUsingsSource =
                "global using System;\n" +
                "global using System.Collections.Generic;\n" +
                "global using System.IO;\n" +
                "global using System.Linq;\n" +
                "global using System.Net.Http;\n" +
                "global using System.Numerics;\n" +
                "global using System.Threading;\n" +
                "global using System.Threading.Tasks;\n";

            var globalUsingsId   = DocumentId.CreateNewId(_projectId, "GlobalUsings");
            var globalUsingsInfo = DocumentInfo.Create(
                globalUsingsId,
                name:           "GlobalUsings.g.cs",
                sourceCodeKind: SourceCodeKind.Regular)
                .WithTextLoader(TextLoader.From(
                    TextAndVersion.Create(SourceText.From(globalUsingsSource), VersionStamp.Create())));

            _workspace.TryApplyChanges(
                _workspace.CurrentSolution.AddDocument(globalUsingsInfo));
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Updates (or lazily creates) the Roslyn document for the given file and
        /// schedules a debounced diagnostic analysis.
        /// </summary>
        /// <param name="debounceMs">Milliseconds to wait before running analysis.</param>
        public void UpdateDocument(string filePath, string sourceText, int debounceMs = 300)
        {
            lock (_lock)
            {
                SourceText text = SourceText.From(sourceText);

                if (_docIds.TryGetValue(filePath, out DocumentId? existingId))
                {
                    _workspace.TryApplyChanges(
                        _workspace.CurrentSolution.WithDocumentText(existingId, text));
                }
                else
                {
                    DocumentId newId = DocumentId.CreateNewId(_projectId, filePath);
                    _docIds[filePath] = newId;
                    DocumentInfo info = DocumentInfo.Create(
                        newId,
                        name:       Path.GetFileName(filePath),
                        filePath:   filePath,
                        sourceCodeKind: SourceCodeKind.Regular)
                        .WithTextLoader(TextLoader.From(TextAndVersion.Create(text, VersionStamp.Create())));
                    _workspace.TryApplyChanges(
                        _workspace.CurrentSolution.AddDocument(info));
                }
            }

            // Cancel any pending analysis and schedule a new one
            CancellationTokenSource cts;
            lock (_lock)
            {
                _analysisCts.Cancel();
                _analysisCts = cts = new CancellationTokenSource();
            }

            _ = Task.Delay(debounceMs, cts.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCanceled) return;
                        _ = AnalyzeAsync(filePath, cts.Token);
                    }, TaskScheduler.Default);
        }

        /// <summary>
        /// Returns the latest diagnostics for the given file (may be empty if not
        /// yet analyzed or if the file has no issues).
        /// </summary>
        public IReadOnlyList<BuildDiagnostic> GetDiagnostics(string filePath)
        {
            lock (_lock)
                return _diagnostics.TryGetValue(filePath, out var d) ? d : Array.Empty<BuildDiagnostic>();
        }

        /// <summary>
        /// Returns completion items for the given 0-based position inside <paramref name="filePath"/>.
        /// The <paramref name="caretOffset"/> must be a byte offset into the source text.
        /// </summary>
        public async Task<IReadOnlyList<CompletionEntry>> GetCompletionsAsync(
            string filePath, int caretOffset, char triggerChar = '\0', CancellationToken ct = default)
        {
            Document? doc = GetDocument(filePath);
            if (doc == null)
            {
                Console.Error.WriteLine($"[Roslyn] GetCompletionsAsync: no doc for '{System.IO.Path.GetFileName(filePath)}' (registered: {_docIds.Count})");
                return Array.Empty<CompletionEntry>();
            }

            try
            {
                CompletionService? svc = CompletionService.GetService(doc);
                if (svc == null)
                {
                    Console.Error.WriteLine($"[Roslyn] GetCompletionsAsync: CompletionService=null");
                    return Array.Empty<CompletionEntry>();
                }

                // Use CreateInsertionTrigger when the user typed a real character so Roslyn
                // returns relevance-ranked results for that char rather than the full A→Z list.
                // Fall back to Invoke (Ctrl+Space) for explicit invocation.
                var trigger = triggerChar != '\0'
                    ? CompletionTrigger.CreateInsertionTrigger(triggerChar)
                    : CompletionTrigger.Invoke;

                CompletionList list = await svc.GetCompletionsAsync(doc, caretOffset, trigger, cancellationToken: ct)
                                               .ConfigureAwait(false);
                if (list == null || list.ItemsList.Count == 0)
                {
                    Console.Error.WriteLine($"[Roslyn] GetCompletionsAsync: list is null or empty");
                    return Array.Empty<CompletionEntry>();
                }

                // No Take() cap — the popup renders 8 rows at a time and UpdateCompletionFilter
                // handles prefix filtering, so list size doesn't impact render performance.
                var completionResults = list.ItemsList
                    .Select(item => new CompletionEntry
                    {
                        Label      = item.DisplayText,
                        Detail     = item.InlineDescription,
                        InsertText = item.DisplayText,
                        Kind       = MapKind(item.Tags)
                    })
                    .ToArray();
                Console.Error.WriteLine($"[Roslyn] GetCompletionsAsync: returning {completionResults.Length} items");
                return completionResults;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Roslyn] GetCompletionsAsync exception: {ex.GetType().Name}: {ex.Message}");
                return Array.Empty<CompletionEntry>();
            }
        }

        // ── Navigation ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the definition location(s) of the symbol at <paramref name="caretOffset"/>.
        /// Falls back to an empty list when Roslyn can't resolve the symbol.
        /// </summary>
        public async Task<IReadOnlyList<SymbolLocation>> GetDefinitionAsync(
            string filePath, int caretOffset, CancellationToken ct = default)
        {
            Document? doc = GetDocument(filePath);
            if (doc == null)
            {
                Console.Error.WriteLine($"[Roslyn] GetDefinitionAsync: no doc for '{System.IO.Path.GetFileName(filePath)}' (registered: {_docIds.Count})");
                return Array.Empty<SymbolLocation>();
            }

            try
            {
                var symbol = await SymbolFinder
                    .FindSymbolAtPositionAsync(doc, caretOffset, ct)
                    .ConfigureAwait(false);
                if (symbol == null)
                {
                    Console.Error.WriteLine($"[Roslyn] GetDefinitionAsync: no symbol at offset {caretOffset} in '{System.IO.Path.GetFileName(filePath)}'");
                    return Array.Empty<SymbolLocation>();
                }
                Console.Error.WriteLine($"[Roslyn] GetDefinitionAsync: symbol='{symbol.Name}' kind={symbol.Kind} locs={symbol.Locations.Length}");

                // For source symbols gather all definition locations.
                // Symbols defined in framework source (compiled via <Compile Include> in Directory.Build.props)
                // will be SourceFile once ScanProjectDirectory has added those files.
                // Before the scan completes they appear as MetadataFile — log that so it's visible.
                var locs = new List<SymbolLocation>();
                foreach (var loc in symbol.Locations)
                {
                    if (loc.Kind == LocationKind.MetadataFile)
                    {
                        Console.Error.WriteLine($"[Roslyn] GetDefinitionAsync: '{symbol.Name}' location is MetadataFile — framework source not yet scanned or not found in workspace");
                        continue;
                    }
                    if (loc.Kind != LocationKind.SourceFile) continue;
                    var span = loc.GetLineSpan();
                    Console.Error.WriteLine($"[Roslyn] GetDefinitionAsync loc: {span.Path} L{span.StartLinePosition.Line + 1}");
                    locs.Add(new SymbolLocation
                    {
                        FilePath = span.Path,
                        Line     = span.StartLinePosition.Line + 1,
                        Column   = span.StartLinePosition.Character + 1,
                        Label    = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                    });
                }
                Console.Error.WriteLine($"[Roslyn] GetDefinitionAsync: returning {locs.Count} source locations (registered docs: {_docIds.Count})");
                return locs;
            }
            catch (OperationCanceledException) { return Array.Empty<SymbolLocation>(); }
            catch (Exception ex)               { Console.Error.WriteLine($"[Roslyn] GetDefinitionAsync ex: {ex.GetType().Name}: {ex.Message}"); return Array.Empty<SymbolLocation>(); }
        }

        /// <summary>
        /// Returns all reference locations of the symbol at <paramref name="caretOffset"/>.
        /// </summary>
        public async Task<IReadOnlyList<SymbolLocation>> GetReferencesAsync(
            string filePath, int caretOffset, CancellationToken ct = default)
        {
            Document? doc = GetDocument(filePath);
            if (doc == null) return Array.Empty<SymbolLocation>();

            try
            {
                var symbol = await SymbolFinder
                    .FindSymbolAtPositionAsync(doc, caretOffset, ct)
                    .ConfigureAwait(false);
                if (symbol == null) return Array.Empty<SymbolLocation>();

                var refGroups = await SymbolFinder
                    .FindReferencesAsync(symbol, _workspace.CurrentSolution, ct)
                    .ConfigureAwait(false);

                string symbolName = symbol.Name;
                var locs = new List<SymbolLocation>();
                foreach (var refGroup in refGroups)
                foreach (var refLoc in refGroup.Locations)
                {
                    var span = refLoc.Location.GetLineSpan();
                    if (string.IsNullOrEmpty(span.Path)) continue;
                    locs.Add(new SymbolLocation
                    {
                        FilePath = span.Path,
                        Line     = span.StartLinePosition.Line + 1,
                        Column   = span.StartLinePosition.Character + 1,
                        Label    = $"{Path.GetFileName(span.Path)}  L{span.StartLinePosition.Line + 1}"
                    });
                }
                return locs;
            }
            catch (OperationCanceledException) { return Array.Empty<SymbolLocation>(); }
            catch (Exception)                  { return Array.Empty<SymbolLocation>(); }
        }

        // ── Signature help ───────────────────────────────────────────────────

        /// <summary>
        /// Returns signature help for the method call enclosing <paramref name="caretOffset"/>,
        /// or <c>null</c> if the caret is not inside a method argument list.
        /// </summary>
        public async Task<SignatureInfo?> GetSignatureHelpAsync(
            string filePath, int caretOffset, CancellationToken ct = default)
        {
            Document? doc = GetDocument(filePath);
            if (doc == null) return null;
            try
            {
                var sourceText = await doc.GetTextAsync(ct).ConfigureAwait(false);
                string src = sourceText.ToString();

                // Walk backwards to find the unclosed '(' for the current argument list.
                int depth = 0, openParen = -1, activeParam = 0;
                for (int i = caretOffset - 1; i >= 0; i--)
                {
                    char c = src[i];
                    if (c == ')' || c == ']') { depth++; continue; }
                    if (c == '(' || c == '[')
                    {
                        if (depth > 0) { depth--; continue; }
                        if (c == '(') { openParen = i; break; }
                        return null;
                    }
                    if (depth == 0 && c == ',') activeParam++;
                    if (c == ';' || c == '{' || c == '}') return null;
                }
                if (openParen < 0) return null;

                int nameEnd = openParen - 1;
                while (nameEnd >= 0 && (src[nameEnd] == ' ' || src[nameEnd] == '\t')) nameEnd--;
                if (nameEnd < 0) return null;

                Console.Error.WriteLine($"[Roslyn] sighelp: caretOffset={caretOffset} openParen={openParen} nameEnd={nameEnd} char='{src[nameEnd]}' activeParam={activeParam}");

                var semanticModel = await doc.GetSemanticModelAsync(ct).ConfigureAwait(false);
                var root          = await doc.GetSyntaxRootAsync(ct).ConfigureAwait(false);
                if (semanticModel == null || root == null)
                {
                    Console.Error.WriteLine("[Roslyn] sighelp: null semanticModel or root");
                    return null;
                }

                IMethodSymbol? methodSym  = null;
                List<IMethodSymbol>? overloadList = null;

                // Strategy 1: find InvocationExpressionSyntax via the '(' token.
                // This is the most reliable approach — works even for incomplete/error code
                // because Roslyn's error recovery still builds the full invocation node.
                var openParenToken = root.FindToken(openParen);
                var invocation     = openParenToken.Parent?
                    .AncestorsAndSelf()
                    .OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault();

                if (invocation != null)
                {
                    var expr = invocation.Expression;
                    Console.Error.WriteLine($"[Roslyn] sighelp: invocation expr={expr.GetType().Name} text='{expr}'");

                    // Try GetMemberGroup on the full expression (most reliable for overloads).
                    var grp = semanticModel.GetMemberGroup(expr, ct).OfType<IMethodSymbol>().ToList();
                    if (grp.Count > 0) { methodSym = grp[0]; overloadList = grp; }

                    // Try exact symbol + candidate symbols.
                    if (methodSym == null)
                    {
                        var si = semanticModel.GetSymbolInfo(expr, ct);
                        if (si.Symbol is IMethodSymbol ms) { methodSym = ms; }
                        else
                        {
                            var cands = si.CandidateSymbols.OfType<IMethodSymbol>().ToList();
                            if (cands.Count > 0) { methodSym = cands[0]; overloadList = cands; }
                        }
                    }

                    // Case-insensitive fallback: resolve receiver type → look up method by name.
                    // Handles the common case where the user is still typing (wrong casing, etc.).
                    if (methodSym == null && expr is MemberAccessExpressionSyntax memberAccess)
                    {
                        string methodName  = memberAccess.Name.Identifier.Text;
                        var    receiverType = semanticModel.GetTypeInfo(memberAccess.Expression, ct).Type
                                             as INamedTypeSymbol;
                        Console.Error.WriteLine($"[Roslyn] sighelp: fallback name='{methodName}' receiver='{receiverType?.Name}'");
                        if (receiverType != null)
                        {
                            var methods = receiverType.GetMembers()
                                .OfType<IMethodSymbol>()
                                .Where(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase)
                                         && m.MethodKind == MethodKind.Ordinary)
                                .ToList();
                            if (methods.Count > 0) { methodSym = methods[0]; overloadList = methods; }
                        }
                    }
                }

                // Strategy 2: token walk from nameEnd (fallback for constructors / simple calls).
                if (methodSym == null)
                {
                    var token = root.FindToken(nameEnd);
                    Console.Error.WriteLine($"[Roslyn] sighelp: token fallback token='{token.Text}' parentType={token.Parent?.GetType().Name}");
                    var node = token.Parent;
                    for (int attempt = 0; attempt < 6 && node != null; attempt++)
                    {
                        var info = semanticModel.GetSymbolInfo(node, ct);
                        if (info.Symbol is IMethodSymbol ms) { methodSym = ms; break; }
                        var cands = info.CandidateSymbols.OfType<IMethodSymbol>().ToList();
                        if (cands.Count > 0) { methodSym = cands[0]; overloadList = cands; break; }
                        var grp = semanticModel.GetMemberGroup(node, ct).OfType<IMethodSymbol>().ToList();
                        if (grp.Count > 0) { methodSym = grp[0]; overloadList = grp; break; }
                        node = node.Parent;
                    }
                }

                Console.Error.WriteLine($"[Roslyn] sighelp: symbol='{methodSym?.Name ?? "null"}' overloads={overloadList?.Count ?? (methodSym != null ? 1 : 0)}");
                if (methodSym == null) return null;

                // Collect all overloads from the containing type
                var allOverloads = overloadList != null
                    ? overloadList
                    : (methodSym.ContainingType
                          ?.GetMembers(methodSym.Name).OfType<IMethodSymbol>().ToList()
                       ?? new List<IMethodSymbol> { methodSym });

                IMethodSymbol best = allOverloads
                    .Where(m => m.Parameters.Length > activeParam)
                    .OrderBy(m => m.Parameters.Length)
                    .FirstOrDefault()
                    ?? allOverloads.OrderByDescending(m => m.Parameters.Length).FirstOrDefault()
                    ?? methodSym;

                string xmlDoc          = best.GetDocumentationCommentXml(preferredCulture: null, expandIncludes: true, cancellationToken: ct) ?? "";
                string summary         = ExtractXmlTag(xmlDoc, "summary");
                Console.Error.WriteLine($"[Roslyn] sighelp: xmlDoc len={xmlDoc.Length} summary='{summary.Replace('\n',' ').Substring(0, Math.Min(80, summary.Length))}'");
                int    activeIdx       = Math.Min(activeParam, best.Parameters.Length - 1);
                string activeParamName = activeIdx >= 0 && activeIdx < best.Parameters.Length
                                         ? best.Parameters[activeIdx].Name : "";
                string activeParamDoc  = ExtractXmlParamDoc(xmlDoc, activeParamName);

                return new SignatureInfo
                {
                    ReturnType     = best.ReturnsVoid ? "void"
                                     : best.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    MethodName     = (best.ContainingType?.Name ?? "") + "." + best.Name,
                    Parameters     = best.Parameters
                        .Select(p => (p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), p.Name))
                        .ToArray(),
                    Summary        = summary,
                    ActiveParamDoc = !string.IsNullOrEmpty(activeParamDoc) ? activeParamDoc : summary,
                    ActiveParam    = Math.Max(0, activeIdx),
                    OverloadCount  = allOverloads.Count
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Roslyn] GetSignatureHelpAsync ex: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        // Returns an XmlDocumentationProvider for the XML file next to a DLL, or null.
        private static DocumentationProvider XmlDocFromSiblingFile(string dllPath)
        {
            string xmlPath = Path.ChangeExtension(dllPath, ".xml");
            return File.Exists(xmlPath)
                ? XmlDocumentationProvider.CreateFromFile(xmlPath)
                : DocumentationProvider.Default;
        }

        // Locates the SDK reference pack directory for the current runtime, which contains
        // both reference-only DLLs and their sibling XML documentation files.
        // e.g. /usr/local/share/dotnet/packs/Microsoft.NETCore.App.Ref/10.0.0/ref/net10.0/
        private static string? FindNetRefPackDirectory(string runtimeDir)
        {
            // runtimeDir: …/shared/Microsoft.NETCore.App/10.0.0  → go up 3 = dotnet root
            string? dotnetRoot = runtimeDir;
            for (int i = 0; i < 3 && dotnetRoot != null; i++)
                dotnetRoot = Path.GetDirectoryName(dotnetRoot);
            if (dotnetRoot == null) return null;

            string packsDir = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
            if (!Directory.Exists(packsDir)) return null;

            string netTfm        = $"net{Environment.Version.Major}.{Environment.Version.Minor}";
            string runtimeVersion = Path.GetFileName(runtimeDir); // "10.0.0"

            // Exact version match first.
            string exactDir = Path.Combine(packsDir, runtimeVersion, "ref", netTfm);
            if (Directory.Exists(exactDir)) return exactDir;

            // Any pack with matching major.minor, take the highest.
            string prefix = $"{Environment.Version.Major}.{Environment.Version.Minor}";
            return Directory.GetDirectories(packsDir)
                .Where(d => Path.GetFileName(d).StartsWith(prefix))
                .Select(d => Path.Combine(d, "ref", netTfm))
                .Where(Directory.Exists)
                .OrderByDescending(d => d)
                .FirstOrDefault();
        }

        // Extracts the text content of the first XML tag <tag>…</tag> from a doc-comment string.
        private static string ExtractXmlTag(string xml, string tag)
        {
            if (string.IsNullOrEmpty(xml)) return "";
            int start = xml.IndexOf($"<{tag}", StringComparison.Ordinal);
            if (start < 0) return "";
            int end = xml.IndexOf($"</{tag}>", start, StringComparison.Ordinal);
            if (end < 0) return "";
            int cs = xml.IndexOf('>', start) + 1;
            if (cs <= 0 || cs >= end) return "";
            return StripXmlTags(xml[cs..end]);
        }

        // Extracts the text content of <param name="paramName">…</param>.
        private static string ExtractXmlParamDoc(string xml, string paramName)
        {
            if (string.IsNullOrEmpty(xml) || string.IsNullOrEmpty(paramName)) return "";
            string open = $"<param name=\"{paramName}\">";
            int start = xml.IndexOf(open, StringComparison.Ordinal);
            if (start < 0) return "";
            int end = xml.IndexOf("</param>", start, StringComparison.Ordinal);
            if (end < 0) return "";
            return xml[(start + open.Length)..end].Trim();
        }

        // Strips XML tags from a string (for displaying doc-comment text).
        private static string StripXmlTags(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            bool inTag = false;
            foreach (char c in s)
            {
                if      (c == '<') { inTag = true;  continue; }
                else if (c == '>') { inTag = false; continue; }
                if (!inTag) sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        // ── Remove document ──────────────────────────────────────────────────

        public void RemoveDocument(string filePath)
        {
            lock (_lock)
            {
                if (!_docIds.TryGetValue(filePath, out DocumentId? id)) return;
                _workspace.TryApplyChanges(_workspace.CurrentSolution.RemoveDocument(id));
                _docIds.Remove(filePath);
                _diagnostics.Remove(filePath);
            }
        }

        // Directories that are never part of the compilable source in an SDK-style project.
        private static readonly HashSet<string> _excludedDirNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "obj", "bin", ".vs", ".git", "wwwroot", "scripts",
            "Android", "ios", "node_modules"
        };

        /// <summary>
        /// Registers every .cs file under <paramref name="directory"/> into the workspace
        /// so that cross-file symbol resolution (Go to Definition, Find References) works
        /// even for files that haven't been opened in the editor yet.
        /// Already-registered files are skipped. Does not trigger diagnostics.
        /// Build-output directories (obj, bin, …) are excluded to avoid duplicate types.
        /// </summary>
        public void ScanProjectDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return;

            var files = new List<string>();
            CollectCsFiles(directory, files);

            // Read source outside the lock to avoid long I/O inside a critical section
            var toAdd = new List<(string filePath, string source)>();
            foreach (string filePath in files)
            {
                lock (_lock) { if (_docIds.ContainsKey(filePath)) continue; }
                string source;
                try { source = File.ReadAllText(filePath, System.Text.Encoding.UTF8); }
                catch { continue; }
                toAdd.Add((filePath, source));
            }

            if (toAdd.Count == 0) return;

            // Apply all new documents in a single atomic TryApplyChanges to avoid
            // the race where concurrent calls each read _workspace.CurrentSolution,
            // both build a "n+1 doc" snapshot from the same "n doc" base, and the
            // second TryApplyChanges is silently rejected by AdhocWorkspace because
            // the solution version has already advanced past what it was built from.
            lock (_lock)
            {
                // Build the list of truly-new docs (double-check inside lock)
                var infos = new List<DocumentInfo>();
                var newIds = new List<(string path, DocumentId id)>();
                foreach (var (filePath, source) in toAdd)
                {
                    if (_docIds.ContainsKey(filePath)) continue;
                    var id = DocumentId.CreateNewId(_projectId, filePath);
                    var info = DocumentInfo.Create(
                        id,
                        name:           Path.GetFileName(filePath),
                        filePath:       filePath,
                        sourceCodeKind: SourceCodeKind.Regular)
                        .WithTextLoader(TextLoader.From(
                            TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())));
                    infos.Add(info);
                    newIds.Add((filePath, id));
                }

                if (infos.Count == 0) return;

                // Build one solution snapshot that adds ALL new docs, then apply once.
                // This also serializes with UpdateDocument/RemoveDocument which both hold _lock.
                Solution solution = _workspace.CurrentSolution;
                foreach (var info in infos)
                    solution = solution.AddDocument(info);

                if (_workspace.TryApplyChanges(solution))
                {
                    foreach (var (path, id) in newIds)
                        _docIds[path] = id;
                }
                // If TryApplyChanges still fails (another holder of _lock beat us), retry
                // one more time from the updated CurrentSolution.
                else
                {
                    solution = _workspace.CurrentSolution;
                    var stillMissing = newIds.Where(p => !_docIds.ContainsKey(p.path)).ToList();
                    foreach (var (_, id) in stillMissing)
                    {
                        var info = infos.First(i => i.Id == id);
                        solution = solution.AddDocument(info);
                    }
                    if (_workspace.TryApplyChanges(solution))
                    {
                        foreach (var (path, id) in stillMissing)
                            _docIds[path] = id;
                    }
                }
            }
        }

        private static void CollectCsFiles(string dir, List<string> result)
        {
            try
            {
                foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.TopDirectoryOnly))
                    result.Add(file);

                foreach (string subDir in Directory.GetDirectories(dir))
                {
                    string name = Path.GetFileName(subDir);
                    if (!_excludedDirNames.Contains(name))
                        CollectCsFiles(subDir, result);
                }
            }
            catch { /* skip inaccessible directories */ }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private Document? GetDocument(string filePath)
        {
            DocumentId? id;
            lock (_lock)
            {
                if (!_docIds.TryGetValue(filePath, out id)) return null;
            }
            return _workspace.CurrentSolution.GetDocument(id);
        }

        private async Task AnalyzeAsync(string filePath, CancellationToken ct)
        {
            Document? doc = GetDocument(filePath);
            if (doc == null) return;

            try
            {
                SemanticModel? model = await doc.GetSemanticModelAsync(ct).ConfigureAwait(false);
                if (model == null || ct.IsCancellationRequested) return;

                var roslynDiags = model.GetDiagnostics(cancellationToken: ct);

                var results = new List<BuildDiagnostic>();
                foreach (Diagnostic d in roslynDiags)
                {
                    if (d.Location.Kind != LocationKind.SourceFile) continue;
                    FileLinePositionSpan span = d.Location.GetLineSpan();
                    results.Add(new BuildDiagnostic
                    {
                        FilePath = filePath,
                        Line     = span.StartLinePosition.Line + 1,   // 1-based
                        Column   = span.StartLinePosition.Character + 1,
                        IsError  = d.Severity == DiagnosticSeverity.Error,
                        Code     = d.Id,
                        Message  = d.GetMessage()
                    });
                }

                lock (_lock)
                    _diagnostics[filePath] = results;

                DiagnosticsChanged?.Invoke(filePath, results);
            }
            catch (OperationCanceledException)
            {
                // analysis was cancelled — no-op
            }
            catch (Exception)
            {
                // swallow unexpected Roslyn errors
            }
        }

        private static CompletionItemKind MapKind(ImmutableArray<string> tags)
            => MapKind((IEnumerable<string>)tags);

        // Roslyn tag string values are plain strings; avoid WellKnownTags dependency
        // to keep this file portable across Roslyn versions.
        private static CompletionItemKind MapKind(IEnumerable<string> tags)
        {
            foreach (string tag in tags)
            {
                return tag switch
                {
                    "Method" or "ExtensionMethod"   => CompletionItemKind.Method,
                    "Class"                         => CompletionItemKind.Class,
                    "Interface"                     => CompletionItemKind.Interface,
                    "Structure" or "Struct"          => CompletionItemKind.Struct,
                    "Enum"                          => CompletionItemKind.Enum,
                    "EnumMember"                    => CompletionItemKind.EnumMember,
                    "Field"                         => CompletionItemKind.Field,
                    "Property"                      => CompletionItemKind.Property,
                    "Event"                         => CompletionItemKind.Event,
                    "Namespace"                     => CompletionItemKind.Namespace,
                    "Keyword"                       => CompletionItemKind.Keyword,
                    "Snippet"                       => CompletionItemKind.Snippet,
                    "Local" or "Parameter"          => CompletionItemKind.Variable,
                    "Constant"                      => CompletionItemKind.Constant,
                    "Constructor"                   => CompletionItemKind.Constructor,
                    _                               => CompletionItemKind.Unknown
                };
            }
            return CompletionItemKind.Unknown;
        }

        // ── IDisposable ──────────────────────────────────────────────────────

        public void Dispose()
        {
            _analysisCts.Cancel();
            _workspace.Dispose();
        }
    }
}
