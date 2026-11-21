using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using DisplayKitDesigner.Parsers;

namespace DisplayKitDesigner
{
    public class UxmlParser
    {
        private int elementCounter = 0;
        private int labelCounter = 0;

        private readonly Dictionary<string, IUxmlElementParser> _parsers;
        private readonly HashSet<string> _fieldNames = new HashSet<string>();

        public UxmlParser()
        {
            _parsers = new Dictionary<string, IUxmlElementParser>
            {
                { "VisualElement", new VisualElementParser(() => elementCounter++) },
                { "Label", new LabelParser(() => labelCounter++) }
            };
        }

        public string GenerateDesignerCode(XDocument doc, string namespaceName, string className)
        {
            var sbFields = new StringBuilder();
            var sbInit = new StringBuilder();

            var root = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "VisualElement");

            if (root != null)
                ParseElement(root, "Canvas", sbFields, sbInit, true);

            var final = new StringBuilder();
            final.AppendLine("using UnityEngine;");
            final.AppendLine("using UnityEngine.UIElements;");
            final.AppendLine("using DisplayKit.Elements;");
            final.AppendLine(string.Empty);
            final.AppendLine($"namespace {namespaceName};");
            final.AppendLine(string.Empty);
            final.AppendLine($"public partial class {className}");
            final.AppendLine("{");

            AppendIndentedBlock(final, sbFields.ToString(), 4);

            final.AppendLine();
            final.AppendLine("    private void InitializeComponent()");
            final.AppendLine("    {");

            AppendIndentedBlock(final, sbInit.ToString(), 8);

            final.AppendLine("    }");
            final.AppendLine("}");

            return final.ToString();
        }

        public string GenerateScriptCode(
            XDocument doc,
            string namespaceName,
            string panelClassName,
            string scriptClassName,
            string scriptDisplayName)
        {
            var sbFields = new StringBuilder();
            var sbInit = new StringBuilder();
            var root = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "VisualElement");

            if (root != null)
                ParseElement(root, "Canvas", sbFields, sbInit, true);

            var final = new StringBuilder();
            final.AppendLine("using DisplayKit.Elements;");
            final.AppendLine("using ScriptLoader;");
            final.AppendLine("using UnityEngine;");
            final.AppendLine("using UnityEngine.UIElements;");
            final.AppendLine();
            final.AppendLine($"namespace {namespaceName};");
            final.AppendLine();
            final.AppendLine($"public class {scriptClassName} : IScript");
            final.AppendLine("{");
            final.AppendLine($"    public string Name {{ get; }} = \"{EscapeCSharpString(scriptDisplayName)}\";");
            final.AppendLine();
            final.AppendLine($"    public {panelClassName} Panel;");
            final.AppendLine();
            final.AppendLine("    public void Enable()");
            final.AppendLine("    {");
            final.AppendLine($"        Panel = new {panelClassName}();");
            final.AppendLine("        Panel?.Canvas?.Spawn();");
            final.AppendLine("    }");
            final.AppendLine();
            final.AppendLine("    public void Disable()");
            final.AppendLine("    {");
            final.AppendLine("        Panel?.Canvas?.Destroy();");
            final.AppendLine("        Panel = null;");
            final.AppendLine("    }");
            final.AppendLine("}");
            final.AppendLine();
            final.AppendLine($"public partial class {panelClassName}");
            final.AppendLine("{");
            final.AppendLine($"    public {panelClassName}()");
            final.AppendLine("    {");
            final.AppendLine("        InitializeComponent();");
            final.AppendLine("    }");
            final.AppendLine();
            AppendIndentedBlock(final, sbFields.ToString(), 4);
            final.AppendLine();
            final.AppendLine("    private void InitializeComponent()");
            final.AppendLine("    {");
            AppendIndentedBlock(final, sbInit.ToString(), 8);
            final.AppendLine("    }");
            final.AppendLine("}");
            return final.ToString();
        }

        private static string EscapeCSharpString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static void AppendIndentedBlock(StringBuilder target, string content, int spaces)
        {
            string indentation = new string(' ', spaces);
            string normalized = content.Replace("\r\n", "\n").TrimEnd();

            foreach (string line in normalized.Split('\n'))
                target.AppendLine(string.IsNullOrWhiteSpace(line) ? string.Empty : indentation + line.TrimEnd());
        }

        public string GetUniqueName(string baseName, Func<int> idGenerator)
        {
            if (!_fieldNames.Contains(baseName))
            {
                _fieldNames.Add(baseName);
                return baseName;
            }

            string uniqueName = $"{baseName}_{idGenerator()}";
            _fieldNames.Add(uniqueName);
            return uniqueName;
        }

        public void ParseElement(XElement element, string parentName, StringBuilder sbFields, StringBuilder sbInit, bool first = false)
        {
            if (_parsers.TryGetValue(element.Name.LocalName, out var parser))
            {
                parser.Parse(element, parentName, sbFields, sbInit, this, first);
            }
            else
            {
                Console.WriteLine("Failed to find parser for element " + element.Name.LocalName);
            }
        }

        public void ParseChildren(XElement parent, string parentVar, StringBuilder sbFields, StringBuilder sbInit)
        {
            foreach (var child in parent.Elements())
            {
                ParseElement(child, parentVar, sbFields, sbInit);
            }
        }
    }
}
