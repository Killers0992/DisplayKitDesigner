using System;
using System.Text;
using System.Xml.Linq;

namespace DisplayKitDesigner.Parsers
{
    public class VisualElementParser : IUxmlElementParser
    {
        private readonly Func<int> _nextId;

        public VisualElementParser(Func<int> nextId)
        {
            _nextId = nextId;
        }

        public void Parse(XElement element, string parentName, StringBuilder sbFields, StringBuilder sbInit, UxmlParser mainParser, bool first = false)
        {
            string nameAttr = element.Attribute("name")?.Value;
            string baseName = nameAttr ?? $"Element{_nextId()}";

            // ask parser for a unique name
            string varName = first ? "Canvas" : mainParser.GetUniqueName(baseName, _nextId);

            // rule: public if explicitly named
            string accessModifier = nameAttr != null || first ? "public" : "private";

            sbFields.AppendLine($"{accessModifier} {(first ? "DisplayCanvas" : "DisplayElement")} {varName};");

            sbInit.AppendLine($"// {varName}");
            sbInit.AppendLine($"{varName} = {(first ? "DisplayCanvas.Create()" : $"{parentName}.AddElement()")};");

            // Attributes
            AttributeParser.ApplyAttributes(element, varName, sbInit);

            sbInit.AppendLine();

            // Children
            mainParser.ParseChildren(element, varName, sbFields, sbInit);
        }
    }
}