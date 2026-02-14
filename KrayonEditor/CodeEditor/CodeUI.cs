using ImGuiNET;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public class RoslynCodeEditor
{
    private string _code = "";
    private int _cursorPos;
    private int _selectedSuggestion;
    private bool _showPopup;
    private bool _needsRefocus;
    private List<CompletionItem> _completionItems = new();
    private AdhocWorkspace _workspace;
    private DocumentId _documentId;
    private ProjectId _projectId;
    private CancellationTokenSource _cts;
    private string _lastWord = "";
    private int _lastCursorPos = -1;
    private bool _isLoadingCompletions;
    private List<MetadataReference> _metadataReferences = new();
    private string _windowName;
    private float _cursorBlinkTime;
    private bool _inputActive;
    private float _scrollY;
    private List<Diagnostic> _diagnostics = new();
    private bool _isAnalyzing;
    private bool _showContextMenu;
    private Vector2 _contextMenuPos;
    private List<CodeAction> _codeActions = new();
    private bool _isLoadingCodeActions;

    // Variables para control de inserciones programáticas
    private string _pendingInsertion = null;
    private int _pendingInsertionPos = -1;
    private bool _skipNextChange = false;

    private const uint BufferSize = 1 << 17;
    private const int MaxSuggest = 8;
    private const float PopupW = 320f;

    static uint C(byte r, byte g, byte b, byte a = 255) =>
        (uint)(a << 24 | b << 16 | g << 8 | r);

    static readonly uint
        ColBg = C(30, 30, 30),
        ColText = C(220, 220, 220),
        ColSubtext = C(150, 150, 150),
        ColBorder = C(60, 60, 60),
        ColPanel = C(40, 40, 40),
        ColCursor = C(255, 255, 255),
        ColError = C(255, 100, 100),
        ColWarning = C(255, 200, 100),

        ColIntelliBg = C(70, 15, 25),
        ColIntelliSelected = C(130, 35, 45),
        ColIntelliHover = C(100, 25, 35),
        ColIntelliBorder = C(150, 50, 60),

        ColKeyword = C(230, 200, 90),
        ColClass = C(200, 180, 100),
        ColInterface = C(180, 220, 120),
        ColMethod = C(240, 240, 240),
        ColProperty = C(220, 220, 220),
        ColField = C(200, 200, 200),
        ColLocal = C(180, 180, 180),
        ColNamespace = C(160, 200, 140),
        ColEnum = C(210, 190, 110),
        ColStruct = C(190, 170, 90),

        ColCodeKeyword = C(200, 160, 60),
        ColCodeString = C(200, 120, 100),
        ColCodeComment = C(100, 150, 100),
        ColCodeNumber = C(180, 200, 180),
        ColCodeType = C(180, 200, 120),
        ColCodeText = C(220, 220, 220);

    private static readonly HashSet<string> Keywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
        "char", "checked", "class", "const", "continue", "decimal", "default",
        "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
        "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
        "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return",
        "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint",
        "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while", "var", "async", "await"
    };

    private static readonly HashSet<string> Types = new()
    {
        "bool", "byte", "sbyte", "char", "decimal", "double", "float",
        "int", "uint", "long", "ulong", "short", "ushort", "object", "string", "void"
    };

    public RoslynCodeEditor(string windowName = "Editor")
    {
        _windowName = windowName;
        InitializeDefaultReferences();
        InitializeWorkspace();
    }

    private void InitializeDefaultReferences()
    {
        _metadataReferences.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        _metadataReferences.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
        _metadataReferences.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
        _metadataReferences.Add(MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location));
        _metadataReferences.Add(MetadataReference.CreateFromFile(typeof(System.Collections.Generic.Dictionary<,>).Assembly.Location));
        _metadataReferences.Add(MetadataReference.CreateFromFile(typeof(System.IO.File).Assembly.Location));
        _metadataReferences.Add(MetadataReference.CreateFromFile(typeof(System.Text.StringBuilder).Assembly.Location));
        _metadataReferences.Add(MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location));
    }

    public void LoadDll(string dllPath)
    {
        try
        {
            var assembly = Assembly.LoadFrom(dllPath);
            var reference = MetadataReference.CreateFromFile(assembly.Location);
            _metadataReferences.Add(reference);
            InitializeWorkspace();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading DLL: {ex.Message}");
        }
    }

    private void InitializeWorkspace()
    {
        _workspace?.Dispose();

        var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
        _workspace = new AdhocWorkspace(host);

        _projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            _projectId,
            VersionStamp.Create(),
            "ScriptProject",
            "ScriptProject",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences: _metadataReferences);

        _workspace.AddProject(projectInfo);

        _documentId = DocumentId.CreateNewId(_projectId);
        var documentInfo = DocumentInfo.Create(
            _documentId,
            "script.cs",
            loader: TextLoader.From(TextAndVersion.Create(
                SourceText.From(_code), VersionStamp.Create())));

        _workspace.AddDocument(documentInfo);
    }

    private void UpdateDocument()
    {
        var document = _workspace.CurrentSolution.GetDocument(_documentId);
        var newDocument = document.WithText(SourceText.From(_code));
        _workspace.TryApplyChanges(newDocument.Project.Solution);
    }

    private async void AnalyzeDiagnosticsAsync()
    {
        if (_isAnalyzing) return;

        _isAnalyzing = true;

        await Task.Run(async () =>
        {
            try
            {
                UpdateDocument();
                var document = _workspace.CurrentSolution.GetDocument(_documentId);
                var semanticModel = await document.GetSemanticModelAsync();

                if (semanticModel != null)
                {
                    var diagnostics = semanticModel.GetDiagnostics();
                    _diagnostics = diagnostics.Where(d =>
                        d.Severity == DiagnosticSeverity.Error ||
                        d.Severity == DiagnosticSeverity.Warning).ToList();
                }
            }
            catch { }
            finally
            {
                _isAnalyzing = false;
            }
        });
    }

    private async void LoadCodeActionsAsync(int position)
    {
        _isLoadingCodeActions = true;
        _codeActions.Clear();

        await Task.Run(async () =>
        {
            try
            {
                UpdateDocument();
                var document = _workspace.CurrentSolution.GetDocument(_documentId);
                var semanticModel = await document.GetSemanticModelAsync();
                var root = await document.GetSyntaxRootAsync();

                if (semanticModel != null && root != null)
                {
                    var diagnostics = semanticModel.GetDiagnostics();
                    var relevantDiagnostics = diagnostics.Where(d =>
                        d.Location.SourceSpan.Contains(position) &&
                        (d.Id == "CS0246" || d.Id == "CS0103" || d.Id == "CS1061")).ToList();

                    foreach (var diagnostic in relevantDiagnostics)
                    {
                        var message = diagnostic.GetMessage();
                        if (message.Contains("'"))
                        {
                            int firstQuote = message.IndexOf('\'');
                            int secondQuote = message.IndexOf('\'', firstQuote + 1);
                            if (secondQuote > firstQuote)
                            {
                                string typeName = message.Substring(firstQuote + 1, secondQuote - firstQuote - 1);

                                var missingNamespace = FindNamespaceForType(typeName);
                                if (!string.IsNullOrEmpty(missingNamespace))
                                {
                                    var action = new AddUsingCodeAction(missingNamespace, typeName);
                                    _codeActions.Add(action);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                _isLoadingCodeActions = false;
            }
        });
    }

    private string FindNamespaceForType(string typeName)
    {
        var commonTypes = new Dictionary<string, string>
        {
            { "List", "System.Collections.Generic" },
            { "Dictionary", "System.Collections.Generic" },
            { "IEnumerable", "System.Collections.Generic" },
            { "IList", "System.Collections.Generic" },
            { "Task", "System.Threading.Tasks" },
            { "File", "System.IO" },
            { "Directory", "System.IO" },
            { "Path", "System.IO" },
            { "StringBuilder", "System.Text" },
            { "Regex", "System.Text.RegularExpressions" },
            { "Thread", "System.Threading" },
            { "Timer", "System.Threading" },
            { "HttpClient", "System.Net.Http" },
            { "JsonSerializer", "System.Text.Json" },
            { "XmlDocument", "System.Xml" },
            { "Enumerable", "System.Linq" },
            { "Debug", "System.Diagnostics" },
            { "Stopwatch", "System.Diagnostics" },
        };

        if (commonTypes.TryGetValue(typeName, out var ns))
        {
            return ns;
        }

        foreach (var reference in _metadataReferences)
        {
            try
            {
                var assembly = Assembly.LoadFrom(reference.Display);
                var type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName && t.IsPublic);
                if (type != null)
                {
                    return type.Namespace;
                }
            }
            catch { }
        }

        return null;
    }

    private async void ApplyCodeActionAsync(CodeAction action)
    {
        try
        {
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            foreach (var operation in operations)
            {
                if (operation is ApplyChangesOperation applyChanges)
                {
                    var newSolution = applyChanges.ChangedSolution;
                    var newDocument = newSolution.GetDocument(_documentId);
                    if (newDocument != null)
                    {
                        var text = await newDocument.GetTextAsync();
                        _code = text.ToString();
                        UpdateDocument();
                        AnalyzeDiagnosticsAsync();
                    }
                }
            }
        }
        catch { }
    }

    private class AddUsingCodeAction : CodeAction
    {
        private readonly string _namespace;
        private readonly string _typeName;

        public AddUsingCodeAction(string ns, string typeName)
        {
            _namespace = ns;
            _typeName = typeName;
        }

        public override string Title => $"using {_namespace};";
        public override string EquivalenceKey => _namespace;

        protected override async Task<Solution> GetChangedSolutionAsync(CancellationToken cancellationToken)
        {
            return null;
        }
    }

    private async void TriggerCompletionAsync(string word, int pos)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _isLoadingCompletions = true;

        await Task.Run(async () =>
        {
            try
            {
                UpdateDocument();
                var document = _workspace.CurrentSolution.GetDocument(_documentId);
                var completionService = CompletionService.GetService(document);

                if (completionService == null) return;

                var completionList = await completionService.GetCompletionsAsync(document, pos, cancellationToken: token);

                if (token.IsCancellationRequested || completionList == null) return;

                var filtered = completionList.ItemsList;

                if (!string.IsNullOrEmpty(word))
                {
                    filtered = filtered
                        .Where(item => item.DisplayText.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(item => !item.DisplayText.StartsWith(word, StringComparison.Ordinal))
                        .ThenBy(item => item.DisplayText.Length)
                        .ThenBy(item => item.DisplayText)
                        .ToList();
                }
                else
                {
                    filtered = filtered.Take(MaxSuggest * 2).ToList();
                }

                if (!token.IsCancellationRequested)
                {
                    _completionItems = filtered.Take(MaxSuggest).ToList();
                    _selectedSuggestion = 0;
                    _showPopup = _completionItems.Count > 0;
                    _isLoadingCompletions = false;
                }
            }
            catch (OperationCanceledException) { }
            catch
            {
                _isLoadingCompletions = false;
            }
        }, token);
    }

    private int WordStart()
    {
        int i = Math.Clamp(_cursorPos, 0, _code.Length) - 1;
        while (i >= 0 && (char.IsLetterOrDigit(_code[i]) || _code[i] == '_')) i--;
        return i + 1;
    }

    private string GetCurrentWord()
    {
        int s = WordStart();
        int len = Math.Clamp(_cursorPos, 0, _code.Length) - s;
        return len > 0 ? _code.Substring(s, len) : "";
    }

    private bool ShouldTriggerCompletion()
    {
        if (_cursorPos > 0 && _cursorPos <= _code.Length)
        {
            char prevChar = _code[_cursorPos - 1];
            if (prevChar == '.')
                return true;
        }

        string word = GetCurrentWord();
        return word.Length >= 1;
    }

    private void ReplaceCurrentWord(string completion)
    {
        int s = WordStart();
        int end = Math.Clamp(_cursorPos, 0, _code.Length);

        string newCode;
        int newCursorPos;

        if (s >= end)
        {
            newCode = _code.Insert(_cursorPos, completion);
            newCursorPos = _cursorPos + completion.Length;
        }
        else
        {
            newCode = _code.Remove(s, end - s);
            newCode = newCode.Insert(s, completion);
            newCursorPos = s + completion.Length;
        }

        _pendingInsertion = newCode;
        _pendingInsertionPos = newCursorPos;
        _showPopup = false;
    }

    private void InsertUsing(string ns)
    {
        string usingStatement = $"using {ns};\n";

        if (_code.Contains(usingStatement.TrimEnd('\n')))
            return;

        int insertPos = 0;
        var lines = _code.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("using "))
            {
                insertPos = _code.IndexOf(lines[i], StringComparison.Ordinal) + lines[i].Length + 1;
            }
            else if (lines[i].Trim().Length > 0 && !lines[i].TrimStart().StartsWith("using "))
            {
                break;
            }
        }

        string newCode = _code.Insert(insertPos, usingStatement);
        int newCursorPos = _cursorPos + usingStatement.Length;

        _pendingInsertion = newCode;
        _pendingInsertionPos = newCursorPos;
    }

    private Vector2 GetCursorScreenPos()
    {
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize();
        float lineHeight = fontSize + ImGui.GetStyle().ItemSpacing.Y;

        int line = 0;
        int col = 0;
        for (int i = 0; i < Math.Min(_cursorPos, _code.Length); i++)
        {
            if (_code[i] == '\n')
            {
                line++;
                col = 0;
            }
            else
            {
                col++;
            }
        }

        var editorPos = ImGui.GetItemRectMin();
        float charWidth = font.CalcTextSizeA(fontSize, float.MaxValue, 0, "A").X;

        float x = editorPos.X + 52f + (col * charWidth);
        float y = editorPos.Y + (line * lineHeight) + lineHeight - _scrollY;

        return new Vector2(x, y);
    }

    private void DrawCursor(Vector2 editorMin, float scrollY)
    {
        _cursorBlinkTime += ImGui.GetIO().DeltaTime;
        if (_cursorBlinkTime > 1.0f) _cursorBlinkTime = 0f;

        bool showCursor = _cursorBlinkTime < 0.5f;
        if (!showCursor || !_inputActive) return;

        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize();
        float lineHeight = fontSize + ImGui.GetStyle().ItemSpacing.Y;

        int line = 0;
        int col = 0;
        for (int i = 0; i < Math.Min(_cursorPos, _code.Length); i++)
        {
            if (_code[i] == '\n')
            {
                line++;
                col = 0;
            }
            else
            {
                col++;
            }
        }

        float charWidth = font.CalcTextSizeA(fontSize, float.MaxValue, 0, "A").X;
        float x = editorMin.X + 8f + (col * charWidth);
        float y = editorMin.Y + 4f + (line * lineHeight) - scrollY;

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(
            new Vector2(x, y),
            new Vector2(x, y + fontSize),
            ColCursor,
            2.0f
        );
    }

    private unsafe int TextEditCallback(ImGuiInputTextCallbackData* data)
    {
        if (data->EventFlag == ImGuiInputTextFlags.CallbackAlways)
        {
            _cursorPos = data->CursorPos;
            _cursorBlinkTime = 0f;
        }

        return 0;
    }

    public unsafe void Draw()
    {
        PushStyle();

        ImGui.SetNextWindowSize(new Vector2(920, 600), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(400, 300), new Vector2(float.MaxValue, float.MaxValue));

        ImGui.Begin(_windowName);

        DrawTitleBar();
        DrawEditorAndLineNumbers();
        DrawAutocomplete();
        DrawContextMenu();
        DrawStatusBar();

        ImGui.End();
        PopStyle();
    }

    private void DrawTitleBar()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ColPanel);
        ImGui.BeginChild("##titlebar", new Vector2(-1, 38), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);

        ImGui.SetCursorPosY(10f);
        ImGui.SetCursorPosX(12f);
        ImGui.Text("script.cs");

        int errors = _diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        int warnings = _diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);

        string diagInfo = "";
        if (errors > 0)
            diagInfo = $"  |  {errors} error(es)";
        if (warnings > 0)
            diagInfo += $"  |  {warnings} advertencia(s)";

        string info = $"Ln {CountLines()}  |  {_code.Length} chars  |  Refs: {_metadataReferences.Count}{diagInfo}";
        ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.CalcTextSize(info).X - 14);

        if (errors > 0)
            ImGui.PushStyleColor(ImGuiCol.Text, ColError);
        else if (warnings > 0)
            ImGui.PushStyleColor(ImGuiCol.Text, ColWarning);
        else
            ImGui.PushStyleColor(ImGuiCol.Text, ColSubtext);

        ImGui.Text(info);
        ImGui.PopStyleColor();

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.Separator();
    }

    private unsafe void DrawEditorAndLineNumbers()
    {
        float lineNumW = 50f;
        float availH = ImGui.GetContentRegionAvail().Y - 26f;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, ColPanel);
        ImGui.BeginChild("##nums", new Vector2(lineNumW, availH), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        int totalLines = CountLines();
        int activeLine = GetActiveLine();

        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize();
        float lineHeight = fontSize + ImGui.GetStyle().ItemSpacing.Y;

        float scrollY = _scrollY;

        int firstVisibleLine = Math.Max(1, (int)(scrollY / lineHeight));
        int lastVisibleLine = Math.Min(totalLines, (int)((scrollY + availH) / lineHeight) + 2);

        ImGui.SetCursorPosY(-scrollY % lineHeight);

        for (int i = firstVisibleLine; i <= lastVisibleLine; i++)
        {
            bool isActive = i == activeLine;
            ImGui.PushStyleColor(ImGuiCol.Text, isActive ? ColText : ColSubtext);
            string n = i.ToString();
            ImGui.SetCursorPosX((lineNumW - ImGui.CalcTextSize(n).X) / 2f);
            ImGui.Text(n);
            ImGui.PopStyleColor();
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.SameLine(0, 0);

        var dl = ImGui.GetWindowDrawList();
        var sp = ImGui.GetCursorScreenPos();
        dl.AddLine(sp, sp + new Vector2(0, availH), ColBorder, 1f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 2f);

        ImGui.PushStyleColor(ImGuiCol.FrameBg, ColBg);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, ColBg);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, ColBorder);
        ImGui.PushStyleColor(ImGuiCol.Text, C(0, 0, 0, 0));

        // Aplicar inserción pendiente ANTES del input
        if (_pendingInsertion != null)
        {
            _code = _pendingInsertion;
            _cursorPos = _pendingInsertionPos;
            _pendingInsertion = null;
            _needsRefocus = true;
            _skipNextChange = true;
            UpdateDocument();
            AnalyzeDiagnosticsAsync();
        }

        if (_needsRefocus)
        {
            ImGui.SetKeyboardFocusHere();
            _needsRefocus = false;
        }

        var rectMin = ImGui.GetCursorScreenPos();
        string previousCode = _code;

        ImGui.InputTextMultiline("##code", ref _code, BufferSize, new Vector2(-1, availH),
            ImGuiInputTextFlags.CallbackAlways,
            TextEditCallback);

        _inputActive = ImGui.IsItemActive();

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            _contextMenuPos = ImGui.GetMousePos();
            _showContextMenu = true;
            LoadCodeActionsAsync(_cursorPos);
        }

        // Manejar cambios del usuario (no programáticos)
        if (_code != previousCode && !_skipNextChange)
        {
            if (_code.Length > previousCode.Length)
            {
                int diff = _code.Length - previousCode.Length;
                if (diff == 1 && _cursorPos > 0)
                {
                    char insertedChar = _code[_cursorPos - 1];
                    if (insertedChar == '{')
                    {
                        _pendingInsertion = _code.Insert(_cursorPos, "}");
                        _pendingInsertionPos = _cursorPos;
                    }
                    else if (insertedChar == '(')
                    {
                        _pendingInsertion = _code.Insert(_cursorPos, ")");
                        _pendingInsertionPos = _cursorPos;
                    }
                }
            }

            if (_pendingInsertion == null)
            {
                UpdateDocument();
                AnalyzeDiagnosticsAsync();
            }
        }

        _skipNextChange = false;
        _scrollY = ImGui.GetScrollY();

        // Manejar teclas especiales
        if (_inputActive)
        {
            if (_showPopup && _completionItems.Count > 0)
            {
                if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
                {
                    _selectedSuggestion = (_selectedSuggestion + 1) % _completionItems.Count;
                }
                else if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
                {
                    _selectedSuggestion = (_selectedSuggestion - 1 + _completionItems.Count) % _completionItems.Count;
                }
                else if (ImGui.IsKeyPressed(ImGuiKey.Tab) || ImGui.IsKeyPressed(ImGuiKey.Enter))
                {
                    ReplaceCurrentWord(_completionItems[_selectedSuggestion].DisplayText);
                }
                else if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    _showPopup = false;
                }
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.Tab))
            {
                _pendingInsertion = _code.Insert(_cursorPos, "    ");
                _pendingInsertionPos = _cursorPos + 4;
            }
        }

        DrawSyntaxHighlighting(rectMin, availH, _scrollY);
        DrawCursor(rectMin, _scrollY);
        DrawDiagnostics(rectMin, availH, _scrollY);

        ImGui.PopStyleColor(4);
    }

    private void DrawContextMenu()
    {
        if (!_showContextMenu) return;

        ImGui.SetNextWindowPos(_contextMenuPos);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, ColIntelliBg);
        ImGui.PushStyleColor(ImGuiCol.Border, ColIntelliBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8f, 8f));

        if (ImGui.Begin("##contextmenu",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            if (_isLoadingCodeActions)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ColSubtext);
                ImGui.Text("Cargando acciones...");
                ImGui.PopStyleColor();
            }
            else if (_codeActions.Count > 0)
            {
                foreach (var action in _codeActions)
                {
                    if (action is AddUsingCodeAction addUsing)
                    {
                        if (ImGui.MenuItem(action.Title))
                        {
                            InsertUsing(addUsing.Title.Replace("using ", "").Replace(";", ""));
                            _showContextMenu = false;
                        }
                    }
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ColSubtext);
                ImGui.Text("No hay acciones disponibles");
                ImGui.PopStyleColor();
            }

            ImGui.Separator();

            if (ImGui.MenuItem("Copiar"))
            {
                ImGui.SetClipboardText(_code);
                _showContextMenu = false;
            }

            if (ImGui.MenuItem("Pegar"))
            {
                string clipboard = ImGui.GetClipboardText();
                if (!string.IsNullOrEmpty(clipboard))
                {
                    _pendingInsertion = _code.Insert(_cursorPos, clipboard);
                    _pendingInsertionPos = _cursorPos + clipboard.Length;
                }
                _showContextMenu = false;
            }

            ImGui.End();
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow))
            {
                _showContextMenu = false;
            }
        }
    }

    private void DrawDiagnostics(Vector2 startPos, float height, float scrollY)
    {
        if (_diagnostics.Count == 0) return;

        var drawList = ImGui.GetWindowDrawList();
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize();
        float lineHeight = fontSize + ImGui.GetStyle().ItemSpacing.Y;

        drawList.PushClipRect(startPos, startPos + new Vector2(ImGui.GetContentRegionAvail().X, height), true);

        foreach (var diagnostic in _diagnostics)
        {
            var span = diagnostic.Location.SourceSpan;
            int line = GetLineFromPosition(span.Start);

            float yPos = startPos.Y + 4f + (line * lineHeight) - scrollY + fontSize;

            uint color = diagnostic.Severity == DiagnosticSeverity.Error ? ColError : ColWarning;

            float xStart = startPos.X + 8f + (GetColumnFromPosition(span.Start) * font.CalcTextSizeA(fontSize, float.MaxValue, 0, "A").X);
            float xEnd = xStart + (span.Length * font.CalcTextSizeA(fontSize, float.MaxValue, 0, "A").X);

            for (float x = xStart; x < xEnd; x += 4f)
            {
                drawList.AddLine(
                    new Vector2(x, yPos),
                    new Vector2(x + 2f, yPos + 2f),
                    color,
                    1f
                );
                drawList.AddLine(
                    new Vector2(x + 2f, yPos + 2f),
                    new Vector2(x + 4f, yPos),
                    color,
                    1f
                );
            }
        }

        drawList.PopClipRect();
    }

    private int GetLineFromPosition(int position)
    {
        int line = 0;
        for (int i = 0; i < Math.Min(position, _code.Length); i++)
        {
            if (_code[i] == '\n') line++;
        }
        return line;
    }

    private int GetColumnFromPosition(int position)
    {
        int col = 0;
        for (int i = position - 1; i >= 0 && i < _code.Length && _code[i] != '\n'; i--)
        {
            col++;
        }
        return col;
    }

    private void DrawSyntaxHighlighting(Vector2 startPos, float height, float scrollY)
    {
        if (string.IsNullOrEmpty(_code)) return;

        var drawList = ImGui.GetWindowDrawList();
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize();
        float lineHeight = fontSize + ImGui.GetStyle().ItemSpacing.Y;

        var lines = _code.Split('\n');

        int firstVisibleLine = Math.Max(0, (int)(scrollY / lineHeight) - 1);
        int lastVisibleLine = Math.Min(lines.Length - 1, (int)((scrollY + height) / lineHeight) + 1);

        drawList.PushClipRect(startPos, startPos + new Vector2(ImGui.GetContentRegionAvail().X, height), true);

        for (int lineIdx = firstVisibleLine; lineIdx <= lastVisibleLine; lineIdx++)
        {
            var line = lines[lineIdx];
            float yPos = startPos.Y + 4f + (lineIdx * lineHeight) - scrollY;
            float xPos = startPos.X + 8f;

            int i = 0;
            while (i < line.Length)
            {
                if (line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    var comment = line.Substring(i);
                    drawList.AddText(new Vector2(xPos, yPos), ColCodeComment, comment);
                    break;
                }
                else if (line[i] == '"')
                {
                    int end = i + 1;
                    while (end < line.Length && line[end] != '"')
                    {
                        if (line[end] == '\\' && end + 1 < line.Length) end++;
                        end++;
                    }
                    if (end < line.Length) end++;

                    var str = line.Substring(i, end - i);
                    var size = font.CalcTextSizeA(fontSize, float.MaxValue, 0, str);
                    drawList.AddText(new Vector2(xPos, yPos), ColCodeString, str);
                    xPos += size.X;
                    i = end;
                }
                else if (char.IsLetter(line[i]) || line[i] == '_')
                {
                    int end = i;
                    while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_'))
                        end++;

                    var word = line.Substring(i, end - i);
                    uint color = ColCodeText;

                    if (Keywords.Contains(word))
                        color = ColCodeKeyword;
                    else if (Types.Contains(word))
                        color = ColCodeType;
                    else if (char.IsUpper(word[0]))
                        color = ColCodeType;

                    var size = font.CalcTextSizeA(fontSize, float.MaxValue, 0, word);
                    drawList.AddText(new Vector2(xPos, yPos), color, word);
                    xPos += size.X;
                    i = end;
                }
                else if (char.IsDigit(line[i]))
                {
                    int end = i;
                    while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.' || line[end] == 'f'))
                        end++;

                    var num = line.Substring(i, end - i);
                    var size = font.CalcTextSizeA(fontSize, float.MaxValue, 0, num);
                    drawList.AddText(new Vector2(xPos, yPos), ColCodeNumber, num);
                    xPos += size.X;
                    i = end;
                }
                else
                {
                    var ch = line[i].ToString();
                    var size = font.CalcTextSizeA(fontSize, float.MaxValue, 0, ch);
                    drawList.AddText(new Vector2(xPos, yPos), ColCodeText, ch);
                    xPos += size.X;
                    i++;
                }
            }
        }

        drawList.PopClipRect();
    }

    private void DrawAutocomplete()
    {
        if (ShouldTriggerCompletion())
        {
            if (_cursorPos != _lastCursorPos)
            {
                _lastCursorPos = _cursorPos;
                string word = GetCurrentWord();
                TriggerCompletionAsync(word, _cursorPos);
            }
        }
        else
        {
            if (_showPopup)
            {
                _showPopup = false;
                _completionItems.Clear();
            }
            _lastCursorPos = -1;
        }

        if (!_showPopup || _completionItems.Count == 0) return;

        var cursorPos = GetCursorScreenPos();

        ImGui.SetNextWindowPos(cursorPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(PopupW, 0));

        ImGui.PushStyleColor(ImGuiCol.WindowBg, ColIntelliBg);
        ImGui.PushStyleColor(ImGuiCol.Border, ColIntelliBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8f, 8f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 3f));

        if (ImGui.Begin("##autocomplete",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings))
        {
            for (int i = 0; i < _completionItems.Count; i++)
            {
                var item = _completionItems[i];
                bool isActive = i == _selectedSuggestion;

                uint textColor = GetColorForKind(item.Tags);

                ImGui.PushStyleColor(ImGuiCol.Header, isActive ? ColIntelliSelected : 0);
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, ColIntelliHover);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, ColIntelliSelected);
                ImGui.PushStyleColor(ImGuiCol.Text, textColor);

                string icon = GetIconForKind(item.Tags);
                ImGui.Selectable($"{icon} {item.DisplayText}", isActive, ImGuiSelectableFlags.None, new Vector2(0, 22));

                ImGui.PopStyleColor(4);
            }

            ImGui.End();
        }

        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(2);
    }

    private string GetIconForKind(ImmutableArray<string> tags)
    {
        if (tags.Contains("Keyword")) return "KW";
        if (tags.Contains("Class")) return "CL";
        if (tags.Contains("Interface")) return "IF";
        if (tags.Contains("Struct")) return "ST";
        if (tags.Contains("Enum")) return "EN";
        if (tags.Contains("Method")) return "MT";
        if (tags.Contains("Property")) return "PR";
        if (tags.Contains("Field")) return "FD";
        if (tags.Contains("Local")) return "LC";
        if (tags.Contains("Namespace")) return "NS";
        return "SY";
    }

    private uint GetColorForKind(ImmutableArray<string> tags)
    {
        if (tags.Contains("Keyword")) return ColKeyword;
        if (tags.Contains("Class")) return ColClass;
        if (tags.Contains("Interface")) return ColInterface;
        if (tags.Contains("Struct")) return ColStruct;
        if (tags.Contains("Enum")) return ColEnum;
        if (tags.Contains("Method")) return ColMethod;
        if (tags.Contains("Property")) return ColProperty;
        if (tags.Contains("Field")) return ColField;
        if (tags.Contains("Local")) return ColLocal;
        if (tags.Contains("Namespace")) return ColNamespace;
        return C(255, 255, 255);
    }

    private void DrawStatusBar()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ColPanel);
        ImGui.BeginChild("##statusbar", new Vector2(-1, 22), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);

        ImGui.SetCursorPosY(3f);
        ImGui.SetCursorPosX(12f);
        ImGui.PushStyleColor(ImGuiCol.Text, ColSubtext);
        ImGui.Text($"Ln {GetActiveLine()}, Col {GetActiveColumn()}   |   C# Roslyn");
        ImGui.PopStyleColor();

        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private void PushStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, ColBg);
        ImGui.PushStyleColor(ImGuiCol.Border, ColBorder);
        ImGui.PushStyleColor(ImGuiCol.Text, ColText);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
    }

    private void PopStyle()
    {
        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(2);
    }

    private int CountLines()
    {
        if (_code.Length == 0) return 1;
        int n = 1;
        foreach (char c in _code) if (c == '\n') n++;
        return n;
    }

    private int GetActiveLine()
    {
        int pos = Math.Clamp(_cursorPos, 0, _code.Length);
        int n = 1;
        for (int i = 0; i < pos; i++)
            if (_code[i] == '\n') n++;
        return n;
    }

    private int GetActiveColumn()
    {
        int pos = Math.Clamp(_cursorPos, 0, _code.Length);
        int col = 1;
        for (int i = pos - 1; i >= 0 && _code[i] != '\n'; i--)
            col++;
        return col;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _workspace?.Dispose();
    }
}