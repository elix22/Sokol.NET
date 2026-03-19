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
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

using GameEditor.CodeEditor;

namespace GameEditor.CodeEditor
{
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
            var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
            _workspace  = new AdhocWorkspace(host);
            _projectId  = ProjectId.CreateNewId("GameProject");

            // Load references: all loaded assemblies that have a physical file
            var refs = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .ToArray();

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
            if (doc == null) return Array.Empty<CompletionEntry>();

            try
            {
                CompletionService? svc = CompletionService.GetService(doc);
                if (svc == null) return Array.Empty<CompletionEntry>();

                CompletionList list = await svc.GetCompletionsAsync(doc, caretOffset, cancellationToken: ct)
                                               .ConfigureAwait(false);
                if (list == null || list.ItemsList.Count == 0) return Array.Empty<CompletionEntry>();

                return list.ItemsList
                    .Take(64)   // cap at 64 for the popup
                    .Select(item => new CompletionEntry
                    {
                        Label      = item.DisplayText,
                        Detail     = item.InlineDescription,
                        InsertText = item.DisplayText,
                        Kind       = MapKind(item.Tags)
                    })
                    .ToArray();
            }
            catch (Exception)
            {
                return Array.Empty<CompletionEntry>();
            }
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
