using DisplayKitDesigner.Parsers;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DisplayKitDesigner
{
    public class DisplayKitDesignerWindow : EditorWindow
    {
        private static readonly string[] RequiredProjectReferences =
        {
            "UnityEngine.UIElementsModule",
            "UnityEngine.TextRenderingModule",
            "UnityEngine.CoreModule",
            "Northwood.SCPSL"
        };

        [SerializeField]
        protected VisualTreeAsset uxmlAsset;

        private UxmlCodegenSettings settings;
        private TextField newUxmlName;
        private TextField newUxmlFolder;
        private VisualElement entriesContainer;
        private IVisualElementScheduledItem projectMonitor;
        private string projectState;

        [MenuItem("Tools/DisplayKit Designer")]
        public static void ShowWindow()
        {
            GetWindow<DisplayKitDesignerWindow>("DisplayKit Designer");
        }

        public void CreateGUI()
        {
            DisplayKitDesignerUpdater.CheckOnToolLaunch();
            projectMonitor?.Pause();
            settings = UxmlCodegenSettings.LoadOrCreate();
            rootVisualElement.Clear();

            uxmlAsset.CloneTree(rootVisualElement);
            ScrollView scroll = rootVisualElement.Q<ScrollView>("content-scroll");

            scroll.Add(BuildAssemblyCard());
            scroll.Add(BuildCreateCard());

            entriesContainer = new VisualElement();
            scroll.Add(entriesContainer);
            RebuildEntries();

            projectState = GetProjectState();
            projectMonitor = rootVisualElement.schedule.Execute(CheckForProjectChanges).Every(1000);
        }

        private void OnFocus()
        {
            if (settings == null || entriesContainer == null)
                return;

            projectState = GetProjectState();
            RebuildEntries();
        }

        private void CheckForProjectChanges()
        {
            string current = GetProjectState();
            if (current == projectState)
                return;

            projectState = current;
            RebuildEntries();
        }

        private string GetProjectState()
        {
            if (settings == null)
                return string.Empty;

            return string.Join("|", settings.entries.Select(entry =>
            {
                try
                {
                    if (!File.Exists(entry.projectFile))
                        return (entry.projectFile ?? string.Empty) + ":missing";

                    FileInfo info = new FileInfo(entry.projectFile);
                    return entry.projectFile + ":" + info.LastWriteTimeUtc.Ticks + ":" + info.Length;
                }
                catch
                {
                    return (entry.projectFile ?? string.Empty) + ":unreadable";
                }
            }));
        }

        private VisualElement BuildAssemblyCard()
        {
            VisualElement card = CreateCard("DisplayKit assembly", "Connect the designer to your installed SCPSL server assembly.");
            VisualElement body = card.Q<VisualElement>("body");

            Button browse = new Button(() =>
            {
                string start = File.Exists(settings.DisplayKitAssemblyPath) ? Path.GetDirectoryName(settings.DisplayKitAssemblyPath) : string.Empty;
                string selected = EditorUtility.OpenFilePanel("Select Northwood.SCPSL.dll", start, "dll");
                if (!string.IsNullOrEmpty(selected))
                {
                    settings.DisplayKitAssemblyPath = selected;
                    SaveSettings();
                    CreateGUI();
                }
            })
            { text = "Browse" };

            body.Add(CreatePathRow("Assembly", CompactTail(settings.DisplayKitAssemblyPath, 2), settings.DisplayKitAssemblyPath, browse));

            bool exists = File.Exists(settings.DisplayKitAssemblyPath);
            body.Add(CreateStatus(
                exists ? "Assembly connected" : "Assembly not found — generation is disabled",
                exists ? "status-success" : "status-error"));
            return card;
        }

        private VisualElement BuildCreateCard()
        {
            VisualElement card = CreateCard("Create a UXML design", "Create a UI Builder document inside this Unity project.");
            VisualElement body = card.Q<VisualElement>("body");

            newUxmlName = new TextField("Design name") { value = "NewDisplay" };
            newUxmlFolder = new TextField("Unity folder") { value = "Assets" };

            Button browse = new Button(() =>
            {
                string selected = EditorUtility.OpenFolderPanel("Select a folder under this project's Assets", Application.dataPath, string.Empty);
                if (string.IsNullOrEmpty(selected))
                    return;

                string assetPath = AbsoluteToAssetPath(selected);
                if (assetPath == null)
                    EditorUtility.DisplayDialog("Invalid folder", "UXML designs must be stored inside this project's Assets folder.", "OK");
                else
                    newUxmlFolder.value = assetPath;
            })
            { text = "Browse" };

            Button create = new Button(CreateUxml) { text = "Create design" };
            create.AddToClassList("primary-button");

            body.Add(newUxmlName);
            body.Add(CreateFieldRow(newUxmlFolder, browse));
            body.Add(create);
            return card;
        }

        private void RebuildEntries()
        {
            if (entriesContainer == null)
                return;

            entriesContainer.Clear();
            VisualElement card = CreateCard("LabAPI connections", "Map each UXML design to a partial C# class in a .NET Framework 4.8 project.");
            VisualElement body = card.Q<VisualElement>("body");

            string[] untracked = FindUntrackedUxmlFiles();
            if (untracked.Length > 0)
            {
                PopupField<string> addExisting = new PopupField<string>("Add existing UXML", new[] { "Choose a design…" }.Concat(untracked).ToList(), 0);
                addExisting.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue != "Choose a design…")
                        AddEntry(evt.newValue);
                });
                body.Add(addExisting);
            }

            if (settings.entries.Count == 0)
                body.Add(CreateStatus("No designs connected yet", "status-neutral"));

            foreach (UxmlCodegenEntry entry in settings.entries.ToArray())
                body.Add(BuildEntry(entry));

            entriesContainer.Add(card);
        }

        private VisualElement BuildEntry(UxmlCodegenEntry entry)
        {
            VisualElement panel = new VisualElement();
            panel.AddToClassList("connection-panel");

            VisualElement heading = new VisualElement();
            heading.AddToClassList("connection-heading");
            VisualElement titleGroup = new VisualElement();
            Label title = new Label(Path.GetFileNameWithoutExtension(entry.uxmlPath));
            title.AddToClassList("connection-title");
            Label path = new Label(entry.uxmlPath);
            path.AddToClassList("muted-label");
            titleGroup.Add(title);
            titleGroup.Add(path);

            Toggle autoGenerate = new Toggle("Generate on save") { value = entry.generateCode };
            autoGenerate.AddToClassList("watch-toggle");
            autoGenerate.tooltip = "Regenerate the designer file whenever this UXML asset changes.";
            autoGenerate.RegisterValueChangedCallback(evt =>
            {
                entry.generateCode = evt.newValue;
                SaveSettings();
            });

            Toggle scriptMode = new Toggle("ScriptLoader file") { value = entry.generateScriptFile };
            scriptMode.AddToClassList("watch-toggle");
            scriptMode.tooltip = "Generate one standalone IScript file instead of a partial .Designer.cs file.";
            scriptMode.RegisterValueChangedCallback(evt =>
            {
                entry.generateScriptFile = evt.newValue;
                SaveSettings();
                RebuildEntries();
            });

            VisualElement modeToggles = new VisualElement();
            modeToggles.AddToClassList("mode-toggles");
            modeToggles.Add(autoGenerate);
            modeToggles.Add(scriptMode);
            heading.Add(titleGroup);
            heading.Add(modeToggles);
            panel.Add(heading);

            Button browseProject = new Button(() =>
            {
                string selected = EditorUtility.OpenFilePanel("Select the .NET Framework 4.8 project", EntryStartFolder(entry), "csproj");
                if (!string.IsNullOrEmpty(selected))
                {
                    ApplyProject(entry, selected);
                    SaveSettings();
                    RebuildEntries();
                }
            })
            { text = "Browse" };

            panel.Add(CreatePathRow(
                "LabAPI project",
                string.IsNullOrEmpty(entry.projectFile) ? "Not selected" : Path.GetFileName(entry.projectFile),
                entry.projectFile,
                browseProject));

            Button browseOutput = new Button(() =>
            {
                string selected = EditorUtility.OpenFolderPanel("Select folder for generated C#", EntryStartFolder(entry), string.Empty);
                if (!string.IsNullOrEmpty(selected))
                {
                    entry.outputFolder = selected;
                    SaveSettings();
                    RebuildEntries();
                }
            })
            { text = "Browse" };
            panel.Add(CreatePathRow(
                "Generated code folder",
                FriendlyRelativePath(entry.outputFolder, ProjectDirectory(entry), true),
                entry.outputFolder,
                browseOutput));

            VisualElement codeRow = new VisualElement();
            codeRow.AddToClassList("two-column-row");

            TextField namespaceField = BoundField("Namespace", entry.namespaceName, value => entry.namespaceName = value);
            namespaceField.AddToClassList("first-column");
            codeRow.Add(namespaceField);
            codeRow.Add(BoundField("Partial class", entry.className, value => entry.className = value));
            panel.Add(codeRow);

            if (entry.generateScriptFile)
            {
                VisualElement scriptRow = new VisualElement();
                scriptRow.AddToClassList("two-column-row");

                TextField scriptClass = BoundField("Script class", entry.scriptClassName, value => entry.scriptClassName = value);
                scriptClass.AddToClassList("first-column");
                scriptRow.Add(scriptClass);
                scriptRow.Add(BoundField("Script name", entry.scriptDisplayName, value => entry.scriptDisplayName = value));
                panel.Add(scriptRow);
            }

            Label generated = new Label(string.IsNullOrEmpty(entry.GeneratedFilePath)
                ? "Generated file not configured"
                : FriendlyRelativePath(entry.GeneratedFilePath, ProjectDirectory(entry), false));

            generated.tooltip = entry.GeneratedFilePath;
            generated.AddToClassList("generated-path");
            panel.Add(generated);

            bool assemblyExists = File.Exists(settings.DisplayKitAssemblyPath);
            bool isNet48 = IsNet48Project(entry.projectFile);

            string[] missingReferences = GetMissingProjectReferences(entry.projectFile, entry.generateScriptFile);

            bool projectReady = File.Exists(entry.projectFile) && isNet48 && missingReferences.Length == 0;
            bool ready = entry.IsValidToGenerate && assemblyExists && projectReady;

            string statusText = ready ? "Ready to generate" : GetEntryProblem(entry, assemblyExists, isNet48, missingReferences);

            panel.Add(CreateStatus(statusText, ready ? "status-success" : "status-warning"));

            VisualElement actions = new VisualElement();
            actions.AddToClassList("action-row");

            Button generate = new Button(() => GenerateCode(settings, entry, true, true)) { text = "Generate now" };
            generate.AddToClassList("primary-button");
            generate.SetEnabled(ready);
            actions.Add(generate);
            actions.Add(new Button(() => OpenInUiBuilder(entry)) { text = "Open in UI Builder" });

            Button openGenerated = new Button(() =>
            {
                if (File.Exists(entry.GeneratedFilePath))
                    EditorUtility.OpenWithDefaultApp(entry.GeneratedFilePath);
            })
            { text = "Open generated" };
            openGenerated.SetEnabled(File.Exists(entry.GeneratedFilePath));
            actions.Add(openGenerated);

            Button remove = new Button(() =>
            {
                settings.entries.Remove(entry);
                SaveSettings();
                RebuildEntries();
            })
            { text = "Remove" };

            remove.AddToClassList("danger-button");
            actions.Add(remove);
            panel.Add(actions);
            return panel;
        }

        private TextField BoundField(string label, string initialValue, Action<string> setter)
        {
            TextField field = new TextField(label) { value = initialValue ?? string.Empty };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                SaveSettings();
            });
            field.RegisterCallback<FocusOutEvent>(_ => RebuildEntries());

            return field;
        }

        private static VisualElement CreateCard(string title, string description)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("section-card");

            VisualElement header = new VisualElement();
            header.AddToClassList("section-header");
            VisualElement copy = new VisualElement();
            Label heading = new Label(title);
            heading.AddToClassList("section-title");
            Label subheading = new Label(description);
            subheading.AddToClassList("section-description");
            copy.Add(heading);
            copy.Add(subheading);
            header.Add(copy);

            VisualElement body = new VisualElement { name = "body" };
            body.AddToClassList("section-body");
            card.Add(header);
            card.Add(body);

            return card;
        }

        private static VisualElement CreateFieldRow(VisualElement field, Button button)
        {
            VisualElement row = new VisualElement();

            row.AddToClassList("field-row");
            field.AddToClassList("grow-field");
            button.AddToClassList("browse-button");
            row.Add(field);
            row.Add(button);

            return row;
        }

        private static VisualElement CreatePathRow(string caption, string displayValue, string fullPath, Button button)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("path-row");

            Label label = new Label(caption);
            label.AddToClassList("path-caption");

            Label value = new Label(string.IsNullOrWhiteSpace(displayValue) ? "Not selected" : displayValue);
            value.AddToClassList("path-value");
            value.tooltip = fullPath ?? string.Empty;

            button.AddToClassList("browse-button");
            row.Add(label);
            row.Add(value);
            row.Add(button);
            return row;
        }

        private static Label CreateStatus(string text, string statusClass)
        {
            Label status = new Label(text);

            status.AddToClassList("status-pill");
            status.AddToClassList(statusClass);

            return status;
        }

        private void CreateUxml()
        {
            if (!UxmlCodegenEntry.IsValidIdentifier(newUxmlName.value) || !AssetDatabase.IsValidFolder(newUxmlFolder.value))
            {
                EditorUtility.DisplayDialog("Invalid design", "Use a valid C#-style name and select an existing folder under Assets.", "OK");
                return;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(newUxmlFolder.value.TrimEnd('/') + "/" + newUxmlName.value + ".uxml");
            const string template = "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" xmlns:uie=\"UnityEditor.UIElements\" editor-extension-mode=\"False\">\n    <ui:VisualElement name=\"Canvas\" style=\"flex-grow: 1;\" />\n</ui:UXML>\n";

            File.WriteAllText(Path.GetFullPath(path), template);
            AssetDatabase.ImportAsset(path);
            AddEntry(path);

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        }

        private void AddEntry(string uxmlPath)
        {
            settings.entries.Add(new UxmlCodegenEntry
            {
                uxmlPath = uxmlPath,
                className = Path.GetFileNameWithoutExtension(uxmlPath),
                namespaceName = "Generated",
                generateCode = true
            });

            SaveSettings();
            RebuildEntries();
        }

        private static void ApplyProject(UxmlCodegenEntry entry, string projectFile)
        {
            entry.projectFile = projectFile;
            entry.outputFolder = Path.GetDirectoryName(projectFile);
            try
            {
                XDocument project = XDocument.Load(projectFile);
                string rootNamespace = project.Descendants().FirstOrDefault(x => x.Name.LocalName == "RootNamespace")?.Value;

                if (string.IsNullOrWhiteSpace(rootNamespace))
                    rootNamespace = Path.GetFileNameWithoutExtension(projectFile);

                entry.namespaceName = rootNamespace.Trim();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Could not read project metadata: " + ex.Message);
            }
        }

        private static void GenerateCode(UxmlCodegenSettings currentSettings, UxmlCodegenEntry entry, bool showResult, bool force)
        {
            if ((!force && !entry.generateCode) || !entry.IsValidToGenerate)
                return;

            try
            {
                AttributeParser.ConfigureAssembly(currentSettings.DisplayKitAssemblyPath);

                XDocument document = XDocument.Load(Path.GetFullPath(entry.uxmlPath));
                UxmlParser parser = new UxmlParser();
                string content = entry.generateScriptFile
                    ? parser.GenerateScriptCode(document, entry.namespaceName, entry.className, entry.scriptClassName, entry.scriptDisplayName)
                    : parser.GenerateDesignerCode(document, entry.namespaceName, entry.className);

                File.WriteAllText(entry.GeneratedFilePath, content);

                if (showResult)
                    EditorUtility.DisplayDialog("DisplayKit Designer", "Generated:\n" + entry.GeneratedFilePath, "OK");

                Debug.Log("Generated DisplayKit code: " + entry.GeneratedFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);

                if (showResult)
                    EditorUtility.DisplayDialog("Generation failed", ex.Message, "OK");
            }
        }

        private static void OpenInUiBuilder(UxmlCodegenEntry entry)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(entry.uxmlPath);
            EditorApplication.ExecuteMenuItem("Window/UI Toolkit/UI Builder");
        }

        private void SaveSettings()
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private string[] FindUntrackedUxmlFiles()
        {
            return AssetDatabase.FindAssets("t:VisualTreeAsset", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => settings.entries.All(entry => entry.uxmlPath != path))
                .ToArray();
        }

        private static string GetEntryProblem(UxmlCodegenEntry entry, bool assemblyExists, bool isNet48, string[] missingReferences)
        {
            if (!assemblyExists)
                return "Select a valid Northwood.SCPSL.dll";

            if (!File.Exists(Path.GetFullPath(entry.uxmlPath)))
                return "UXML design cannot be found";

            if (!File.Exists(entry.projectFile))
                return "Select a valid LabAPI .csproj";

            if (!isNet48)
                return "Project must target net48 / .NET Framework 4.8";

            if (missingReferences.Length > 0)
                return "Missing project references: " + string.Join(", ", missingReferences);

            if (!Directory.Exists(entry.outputFolder))
                return "Select the generated code folder";

            if (!UxmlCodegenEntry.IsValidIdentifier(entry.className))
                return "Enter a valid partial class name";

            if (entry.generateScriptFile && !UxmlCodegenEntry.IsValidIdentifier(entry.scriptClassName))
                return "Enter a valid ScriptLoader class name";

            return "Enter a valid namespace";
        }

        private static string[] GetMissingProjectReferences(string projectFile, bool requireScriptLoader)
        {
            string[] requiredReferences = requireScriptLoader
                ? RequiredProjectReferences.Concat(new[] { "ScriptLoader" }).ToArray()
                : RequiredProjectReferences;

            if (!File.Exists(projectFile))
                return requiredReferences;

            try
            {
                XDocument project = XDocument.Load(projectFile);
                string[] referencedAssemblies = project.Descendants()
                    .Where(element => element.Name.LocalName == "Reference")
                    .SelectMany(element =>
                    {
                        string include = element.Attribute("Include")?.Value ?? string.Empty;
                        string hintPath = element.Elements().FirstOrDefault(child => child.Name.LocalName == "HintPath")?.Value ?? string.Empty;

                        return new[]
                        {
                            NormalizeAssemblyName(include.Split(',')[0]),
                            NormalizeAssemblyName(hintPath)
                        };
                    })
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return requiredReferences
                    .Where(required => !referencedAssemblies.Contains(required, StringComparer.OrdinalIgnoreCase))
                    .ToArray();
            }
            catch
            {
                return requiredReferences;
            }
        }

        private static string NormalizeAssemblyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim().Replace('\\', '/');
            normalized = normalized.Substring(normalized.LastIndexOf('/') + 1);

            return normalized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(0, normalized.Length - 4)
                : normalized;
        }

        private static bool IsNet48Project(string projectFile)
        {
            if (!File.Exists(projectFile))
                return false;

            try
            {
                XDocument project = XDocument.Load(projectFile);

                return project.Descendants().Any(x =>
                    (x.Name.LocalName == "TargetFramework" && x.Value.Split(';').Any(v => v.Trim().Equals("net48", StringComparison.OrdinalIgnoreCase))) ||
                    (x.Name.LocalName == "TargetFrameworkVersion" && x.Value.Trim().Equals("v4.8", StringComparison.OrdinalIgnoreCase)));
            }
            catch
            {
                return false;
            }
        }

        private static string EntryStartFolder(UxmlCodegenEntry entry)
        {
            if (Directory.Exists(entry.outputFolder))
                return entry.outputFolder;

            if (File.Exists(entry.projectFile))
                return Path.GetDirectoryName(entry.projectFile);

            return string.Empty;
        }

        private static string ProjectDirectory(UxmlCodegenEntry entry)
        {
            return File.Exists(entry.projectFile) ? Path.GetDirectoryName(entry.projectFile) : string.Empty;
        }

        private static string FriendlyRelativePath(string path, string baseFolder, bool useCurrentFolderName)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "Not selected";

            try
            {
                if (!string.IsNullOrWhiteSpace(baseFolder))
                {
                    string baseUriPath = Path.GetFullPath(baseFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    Uri baseUri = new Uri(baseUriPath);
                    string targetPath = Path.GetFullPath(path);

                    if (useCurrentFolderName)
                        targetPath = targetPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                    string relative = Uri.UnescapeDataString(baseUri.MakeRelativeUri(new Uri(targetPath)).ToString()).TrimEnd('/');

                    if (!relative.StartsWith("..", StringComparison.Ordinal))
                        return string.IsNullOrEmpty(relative) ? "." : relative.Replace('/', Path.DirectorySeparatorChar);
                }
            }
            catch
            {
            }

            return CompactTail(path, useCurrentFolderName ? 1 : 2);
        }

        private static string CompactTail(string path, int segmentCount)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "Not selected";

            string[] segments = path.Replace('\\', '/').TrimEnd('/').Split('/');

            return string.Join("/", segments.Skip(Math.Max(0, segments.Length - segmentCount)));
        }

        private static string AbsoluteToAssetPath(string folder)
        {
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!full.Equals(assets, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return null;

            return "Assets" + full.Substring(assets.Length).Replace('\\', '/');
        }

        private class UxmlWatcher : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                UxmlCodegenSettings current = UxmlCodegenSettings.LoadOrCreate();

                foreach (UxmlCodegenEntry entry in current.entries.Where(e => e.generateCode && importedAssets.Contains(e.uxmlPath)))
                    GenerateCode(current, entry, false, false);
            }
        }
    }
}
