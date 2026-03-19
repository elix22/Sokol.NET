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

            // Load references: scan the .NET runtime directory for ALL framework DLLs.
            // Using AppDomain alone is insufficient — System.Runtime.dll is a type-forwarding
            // facade in modern .NET, so Roslyn can't resolve types like MathF through it.
            // Loading from the runtime directory includes System.Private.CoreLib.dll (where
            // MathF, Math, etc. actually live) and all the standard library facades.
            string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            var runtimeRefs = Directory
                .GetFiles(runtimeDir, "*.dll", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    string name = Path.GetFileName(f);
                    return !name.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase)
                        && !name.Contains(".resources.", StringComparison.OrdinalIgnoreCase);
                })
                .Select(f => (MetadataReference)MetadataReference.CreateFromFile(f));

            // Also add non-BCL assemblies already loaded in this AppDomain (game framework,
            // Sokol bindings, etc.) that live outside the runtime directory.
            var domainRefs = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a =>
                {
                    if (a.IsDynamic || string.IsNullOrEmpty(a.Location)) return false;
                    string dir = Path.GetDirectoryName(a.Location) ?? "";
                    return !string.Equals(dir, runtimeDir, StringComparison.OrdinalIgnoreCase);
                })
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location));

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
            string filePath, int caretOffset, CancellationToken ct = default)
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

                CompletionList list = await svc.GetCompletionsAsync(doc, caretOffset, cancellationToken: ct)
                                               .ConfigureAwait(false);
                if (list == null || list.ItemsList.Count == 0)
                {
                    Console.Error.WriteLine($"[Roslyn] GetCompletionsAsync: list is null or empty");
                    return Array.Empty<CompletionEntry>();
                }

                var completionResults = list.ItemsList
                    .Take(64)   // cap at 64 for the popup
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

            foreach (string filePath in files)
            {
                bool alreadyKnown;
                lock (_lock) alreadyKnown = _docIds.ContainsKey(filePath);
                if (alreadyKnown) continue;

                string sourceText;
                try { sourceText = File.ReadAllText(filePath, System.Text.Encoding.UTF8); }
                catch { continue; }

                lock (_lock)
                {
                    if (_docIds.ContainsKey(filePath)) continue;   // double-check inside lock

                    DocumentId newId = DocumentId.CreateNewId(_projectId, filePath);
                    _docIds[filePath] = newId;
                    var info = DocumentInfo.Create(
                        newId,
                        name:           Path.GetFileName(filePath),
                        filePath:       filePath,
                        sourceCodeKind: SourceCodeKind.Regular)
                        .WithTextLoader(TextLoader.From(
                            TextAndVersion.Create(SourceText.From(sourceText), VersionStamp.Create())));
                    _workspace.TryApplyChanges(_workspace.CurrentSolution.AddDocument(info));
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
