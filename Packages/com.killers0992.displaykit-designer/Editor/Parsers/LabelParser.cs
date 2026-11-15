using System;
using System.Text;
using System.Xml.Linq;

namespace DisplayKitDesigner.Parsers
{
    public class LabelParser : IUxmlElementParser
    {
        private readonly Func<int> _nextId;

        public LabelParser(Func<int> nextId)
        {
            _nextId = nextId;
        }

        public void Parse(XElement element, string parentName, StringBuilder sbFields, StringBuilder sbInit, UxmlParser mainParser, bool first = false)
        {
            string nameAttr = element.Attribute("name")?.Value;
            string baseName = nameAttr ?? $"Label{_nextId()}";

            // ask parser for a unique name
            string varName = mainParser.GetUniqueName(baseName, _nextId);

            // rule: public if explicitly named
            string accessModifier = nameAttr != null ? "public" : "private";

            sbFields.AppendLine($"{accessModifier} DisplayText {varName};");

            sbInit.AppendLine("///");
            sbInit.AppendLine($"/// {varName}");
            sbInit.AppendLine("///");
            sbInit.AppendLine($"{varName} = {parentName}.AddText();");

            // Attributes
            AttributeParser.ApplyAttributes(element, varName, sbInit);

            sbInit.AppendLine();
        }
    }
}