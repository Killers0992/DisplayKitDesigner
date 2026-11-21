using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Mono.Cecil;
using UnityEngine;
using UnityEngine.UIElements;

namespace DisplayKitDesigner.Parsers
{
    public static class AttributeParser
    {
        public class StylePropertyMapping
        {
            public string CssKey { get; set; } = "";
            public string PropertyPath { get; set; } = "";
            public Type PropertyType { get; set; } = typeof(object);

            public Type EnumType { get; set; } = typeof(string);
            public string EnumTypeName { get; set; } = "";
            public string[] EnumNames { get; set; } = Array.Empty<string>();

            public string ValueToCode(string value)
            {
                if (PropertyType == typeof(StyleFloat))
                    return ParseFloat(value);

                if (PropertyType == typeof(StyleLength))
                    return ParseLength(value);

                if (PropertyType == typeof(StyleColor))
                    return ParseColor(value);

                if (PropertyType == typeof(StyleScale))
                    return ParseScale(value);

                if (PropertyType == typeof(StyleTransformOrigin))
                    return ParseTransformOrigin(value);

                if (PropertyType == typeof(StyleTranslate))
                    return ParseTranslate(value);

                if (PropertyType == typeof(StyleTextShadow))
                    return ParseTextShadow(value);

                if (PropertyType == typeof(StyleEnum<>))
                    return ParseEnum(EnumTypeName, EnumNames, value);

                if (PropertyType == typeof(StyleRotate))
                    return ParseRotate(value);

                Console.WriteLine("[UxmlToCode] Missing parser for " + PropertyType.FullName);
                return "INVALID";
            }
        }

        private static Dictionary<string, StylePropertyMapping> _mappings;
        private static string _assemblyPath;

        public static void ConfigureAssembly(string assemblyPath)
        {
            string normalized = string.IsNullOrWhiteSpace(assemblyPath) ? null : Path.GetFullPath(assemblyPath);
            if (string.Equals(_assemblyPath, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            _assemblyPath = normalized;
            _mappings = null;
        }

        /// <summary>
        /// Builds and caches mappings of CSS keys to Element property paths and types.
        /// </summary>
        public static IReadOnlyDictionary<string, StylePropertyMapping> GetMappings()
        {
            if (_mappings != null)
                return _mappings;

            _mappings = new Dictionary<string, StylePropertyMapping>();

            if (string.IsNullOrWhiteSpace(_assemblyPath) || !File.Exists(_assemblyPath))
                throw new FileNotFoundException("Select Northwood.SCPSL.dll in Tools > DisplayKit Designer before generating code.", _assemblyPath);

            DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(_assemblyPath));
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(_assemblyPath, new ReaderParameters { AssemblyResolver = resolver });

            foreach (TypeDefinition type in assembly.MainModule.Types)
            {
                if (type.IsAbstract)
                    continue;

                if (!(type.BaseType is GenericInstanceType baseType))
                    continue;

                if (baseType.ElementType.Name != "Element`1")
                    continue;

                foreach (PropertyDefinition dataProp in GetPropertiesIncludingBaseTypes(type))
                {
                    TypeDefinition dataType = SafeResolve(dataProp.PropertyType);
                    string baseTypeName = dataType?.BaseType?.Name;

                    if (baseTypeName != "DirtyDataBase")
                        continue;

                    string addText = string.Empty;

                    switch (dataProp.PropertyType.Name)
                    {
                        case "BorderData":
                            addText = $"border-";
                            break;
                        case "BackgroundData":
                            addText = "background-";
                            break;
                        case "FlexData":
                            addText = "flex-";
                            break;
                    }

                    foreach (PropertyDefinition styleProp in dataType.Properties)
                    {
                        if (!styleProp.PropertyType.Name.StartsWith("Style"))
                            continue;

                        string propertyPath = $"{dataProp.Name}.{styleProp.Name}";
                        string overrideName = null;
                        string additionalText = null;

                        switch (styleProp.Name)
                        {
                            case "OutlineWidth":
                            case "OutlineColor":
                            case "Align":
                                additionalText = "-unity-text-";
                                break;
                            case "ParagraphSpacing":
                            case "FontStyle":
                                additionalText = "-unity-";
                                break;
                        }

                        switch (propertyPath)
                        {
                            case "Text.Wrap":
                                overrideName = "white-space";
                                break;
                            case "Text.Overflow":
                                overrideName = "text-overflow";
                                break;
                        }


                        string cssKey = overrideName == null ? $"{(additionalText != null ? additionalText : addText)}" + ToKebabCase(styleProp.Name) : overrideName;

                        if (!_mappings.ContainsKey(cssKey))
                        {
                            _mappings[cssKey] = new StylePropertyMapping
                            {
                                CssKey = cssKey,
                                PropertyPath = propertyPath,
                                PropertyType = GetRuntimeStyleType(styleProp.PropertyType),
                                EnumTypeName = GetEnumTypeName(styleProp.PropertyType),
                                EnumNames = GetEnumNames(styleProp.PropertyType)
                            };
                        }
                    }
                }
            }

            return _mappings;
        }

        private static TypeDefinition SafeResolve(TypeReference type)
        {
            try
            {
                return type.Resolve();
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<PropertyDefinition> GetPropertiesIncludingBaseTypes(TypeDefinition type)
        {
            HashSet<string> seen = new HashSet<string>();
            TypeDefinition current = type;
            while (current != null)
            {
                foreach (PropertyDefinition property in current.Properties)
                {
                    if (seen.Add(property.Name))
                        yield return property;
                }

                current = current.BaseType == null ? null : SafeResolve(current.BaseType);
            }
        }

        private static Type GetRuntimeStyleType(TypeReference type)
        {
            string name = type is GenericInstanceType generic ? generic.ElementType.Name : type.Name;
            switch (name)
            {
                case "StyleFloat":
                    return typeof(StyleFloat);

                case "StyleLength":
                    return typeof(StyleLength);

                case "StyleColor":
                    return typeof(StyleColor);

                case "StyleScale":
                    return typeof(StyleScale);

                case "StyleTransformOrigin":
                    return typeof(StyleTransformOrigin);

                case "StyleTranslate":
                    return typeof(StyleTranslate);

                case "StyleTextShadow":
                    return typeof(StyleTextShadow);

                case "StyleEnum`1":
                    return typeof(StyleEnum<>);

                case "StyleRotate":
                    return typeof(StyleRotate);

                default: return typeof(object);
            }
        }

        private static string GetEnumTypeName(TypeReference type)
        {
            return type is GenericInstanceType generic && generic.ElementType.Name == "StyleEnum`1"
                ? generic.GenericArguments[0].Name
                : string.Empty;
        }

        private static string[] GetEnumNames(TypeReference type)
        {
            if (!(type is GenericInstanceType generic) || generic.ElementType.Name != "StyleEnum`1")
                return Array.Empty<string>();

            TypeDefinition enumType = SafeResolve(generic.GenericArguments[0]);
            return enumType == null
                ? Array.Empty<string>()
                : enumType.Fields.Where(field => field.IsStatic).Select(field => field.Name).ToArray();
        }

        private static string ToKebabCase(string name)
        {
            return string.Concat(
                name.Select((c, i) => i > 0 && char.IsUpper(c) ? "-" + char.ToLower(c) : char.ToLower(c).ToString())
            );
        }

        public static void ApplyAttributes(XElement element, string varName, StringBuilder sbInit)
        {
            var map = GetMappings();

            foreach (var attr in element.Attributes())
            {
                switch (attr.Name.LocalName)
                {
                    case "text":
                        sbInit.AppendLine($"{varName}.Content = \"{attr.Value.EscapeQuotes()}\";");
                        break;

                    case "style":
                        string[] styleAttributes = attr.Value.Split(";");

                        foreach (var styleAttribute in styleAttributes)
                        {
                            if (string.IsNullOrEmpty(styleAttribute))
                                continue;

                            string attribute = styleAttribute.Trim();

                            string[] keyValue = attribute.Split(new[] { ':' }, 2);

                            if (keyValue.Length != 2)
                                continue;

                            string key = keyValue[0];
                            string value = keyValue[1].Trim();

                            if (map.TryGetValue(key, out StylePropertyMapping mapping))
                            {
                                sbInit.AppendLine($"{varName}.{mapping.PropertyPath} = {mapping.ValueToCode(value)};");
                                continue;
                            }
                            else
                            {
                                sbInit.AppendLine($"// Not found {key}: {value}");
                                continue;
                            }
                        }
                        break;
                }
            }
        }

        static string ParseLength(string value, bool withoutSuffixAndPrefix = false)
        {
            if (value.EndsWith("px"))
            {
                if (withoutSuffixAndPrefix)
                    return value.Substring(0, value.Length - 2);

                return $"new Length({value.Substring(0, value.Length - 2)}f, LengthUnit.Pixel)";
            }
            else if (value.EndsWith("%"))
                return $"new Length({value.Substring(0, value.Length - 1)}f, LengthUnit.Percent)";

            if (ParseStyle(value, out string style))
                return style;

            return $"{value}";
        }

        static string ParseEnum(string enumTypeName, IEnumerable<string> enumNames, string value)
        {
            if (ParseStyle(value, out string style))
                return style;

            string normalized = value.Replace("-", "").Replace("_", "").ToLower();
            foreach (string name in enumNames)
            {
                if (name.Replace("_", "").Replace("-", "").ToLower() == normalized)
                    return $"{enumTypeName}.{name}";
            }

            return $"INVALID /* {value} */";
        }

        static string ParseColor(string value)
        {
            if (ParseStyle(value, out string style))
                return style;

            Match rgb = Regex.Match(
                value,
                @"^rgba?\(\s*(?<r>\d{1,3})\s*,\s*(?<g>\d{1,3})\s*,\s*(?<b>\d{1,3})(?:\s*,\s*(?<a>(?:\d+(?:\.\d+)?|\.\d+)%?))?\s*\)$",
                RegexOptions.IgnoreCase);

            if (rgb.Success)
            {
                if (!int.TryParse(rgb.Groups["r"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int red) || red > 255)
                    return $"INVALID /* {value} */";

                if (!int.TryParse(rgb.Groups["g"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int green) || green > 255)
                    return $"INVALID /* {value} */";

                if (!int.TryParse(rgb.Groups["b"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int blue) || blue > 255)
                    return $"INVALID /* {value} */";

                float fRed = red / 255f;
                float fGreen = green / 255f;
                float fBlue = blue / 255f;
                float alpha = 1f;

                if (rgb.Groups["a"].Success)
                {
                    string alphaText = rgb.Groups["a"].Value;
                    bool percentage = alphaText.EndsWith("%", StringComparison.Ordinal);
                    if (percentage)
                        alphaText = alphaText.Substring(0, alphaText.Length - 1);

                    if (!float.TryParse(alphaText, NumberStyles.Float, CultureInfo.InvariantCulture, out alpha))
                        return $"INVALID /* {value} */";

                    if (percentage)
                        alpha /= 100f;

                    if (alpha < 0f || alpha > 1f)
                        return $"INVALID /* {value} */";
                }

                return $"new Color({FormatColorChannel(fRed)}, {FormatColorChannel(fGreen)}, {FormatColorChannel(fBlue)}, {FormatColorChannel(alpha)})";
            }

            if (ColorUtility.TryParseHtmlString(value, out _))
                return $"ColorUtility.TryParseHtmlString(\"{value}\", out var c) ? c : Color.white";

            return $"INVALID /* {value} */";
        }

        private static string FormatColorChannel(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture) + "f";
        }

        static string ParseTextShadow(string value)
        {
            if (ParseStyle(value, out string style))
                return style;

            string pattern = @"^(?<offsetX>\d+px)\s+" +
                             @"(?<offsetY>\d+px)\s+" +
                             @"(?<blur>\d+px)\s+" +
                             @"(?<color>rgb\(\d+,\s*\d+,\s*\d+\))$";

            var match = Regex.Match(value, pattern);
            if (match.Success)
            {
                string offsetX = match.Groups["offsetX"].Value.Trim();
                string offsetY = match.Groups["offsetY"].Value.Trim();
                string blur = match.Groups["blur"].Value.Trim();
                string color = match.Groups["color"].Success ? match.Groups["color"].Value : "none";

                return @"new TextShadow() { offset = " + $"new Vector2({ParseFloat(offsetX, true)}, {ParseFloat(offsetY, true)}), blurRadius = " + $"{ParseFloat(offsetY, true)}, color = " + ParseColor(color) + " }";
            }

            return $"INVALID /* {value} */";
        }

        static string ParseTransformOrigin(string value)
        {
            if (ParseStyle(value, out string style))
                return style;

            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return $"INVALID /* {value} */";

            string x = ParseLength(parts[0]);
            string y = ParseLength(parts[1]);

            return $"new TransformOrigin({x}, {y}, 0f)";
        }

        static string ParseTranslate(string value)
        {
            if (ParseStyle(value, out string style))
                return style;

            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return $"INVALID /* {value} */";

            string x = ParseLength(parts[0]);
            string y = ParseLength(parts[1]);

            return $"new Translate({x}, {y}, 0f)";
        }

        static string ParseScale(string value)
        {
            if (ParseStyle(value, out string style))
                return style;

            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return $"new Vector2({parts[0]}f, {parts[0]}f)";
            if (parts.Length == 2)
                return $"new Vector2({parts[0]}f, {parts[1]}f)";

            return $"INVALID /* {value} */";
        }

        static string ParseRotate(string value)
        {
            if (ParseStyle(value, out string style))
                return style;

            if (value.EndsWith("deg"))
                return $"new UnityEngine.UIElements.Rotate(new Angle({value[..^3]}f, AngleUnit.Degree))";
            else if (value.EndsWith("grad"))
                return $"new UnityEngine.UIElements.Rotate(new Angle({value[..^4]}f, AngleUnit.Gradian))";
            else if (value.EndsWith("rad"))
                return $"new UnityEngine.UIElements.Rotate(new Angle({value[..^3]}f, AngleUnit.Radian))";
            else if (value.EndsWith("turn"))
                return $"new UnityEngine.UIElements.Rotate(new Angle({value[..^4]}f, AngleUnit.Turn))";

            return $"INVALID /* {value} */";
        }

        static string ParseFloat(string value, bool parseWithoutPrefixAndSuffix = true)
        {
            if (ParseStyle(value, out string style))
                return style;

            if (value.EndsWith("px"))
                return $"{ParseLength(value, parseWithoutPrefixAndSuffix)}f";

            return $"{value}f";
        }

        static bool ParseStyle(string value, out string outputValue)
        {
            switch (value)
            {
                case "initial":
                    outputValue = "StyleKeyword.Initial";
                    return true;

                case "auto":
                    outputValue = "StyleKeyword.Auto";
                    return true;

                case "hidden":
                case "none":
                    outputValue = "StyleKeyword.None";
                    return true;
            }

            outputValue = null;
            return false;
        }
    }
}
