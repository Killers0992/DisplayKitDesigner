using System.Text;
using System.Xml.Linq;

namespace DisplayKitDesigner.Parsers
{
    public interface IUxmlElementParser
    {
        void Parse(XElement element, string parentName, StringBuilder sbFields, StringBuilder sbInit, UxmlParser mainParser, bool first = false);
    }
}