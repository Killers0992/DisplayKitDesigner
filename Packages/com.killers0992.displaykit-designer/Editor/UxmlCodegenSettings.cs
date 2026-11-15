using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DisplayKitDesigner
{
    [Serializable]
    public class UxmlCodegenEntry
    {
        public string uxmlPath;
        public string projectFile;
        public string outputFolder;
        public string namespaceName = "Generated";
        public string className;
        public bool generateCode = true;
        public bool generateScriptFile;
        public string scriptClassName = "MainClass";
        public string scriptDisplayName = "Display Script";

        public string GeneratedFilePath => string.IsNullOrWhiteSpace(outputFolder) || string.IsNullOrWhiteSpace(className)
            ? string.Empty
            : Path.Combine(outputFolder, generateScriptFile ? scriptClassName + ".cs" : className + ".Designer.cs");

        public bool IsValidToGenerate =>
            !string.IsNullOrWhiteSpace(uxmlPath) &&
            File.Exists(Path.GetFullPath(uxmlPath)) &&
            Directory.Exists(outputFolder) &&
            IsValidIdentifier(className) &&
            (!generateScriptFile || IsValidIdentifier(scriptClassName)) &&
            IsValidNamespace(namespaceName);

        public static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !(char.IsLetter(value[0]) || value[0] == '_'))
                return false;

            return value.Skip(1).All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        private static bool IsValidNamespace(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Split('.').All(IsValidIdentifier);
        }
    }

    public class UxmlCodegenSettings : ScriptableObject
    {
        public const string DefaultScpslAssembly = @"C:\Program Files (x86)\Steam\steamapps\common\SCP Secret Laboratory Dedicated Server\SCPSL_Data\Managed\Northwood.SCPSL.dll";
        private const string SettingsPath = "Assets/Editor/UxmlCodegenSettings.asset";

        public string DisplayKitAssemblyPath = DefaultScpslAssembly;
        public List<UxmlCodegenEntry> entries = new List<UxmlCodegenEntry>();

        public static UxmlCodegenSettings LoadOrCreate()
        {
            UxmlCodegenSettings settings = AssetDatabase.LoadAssetAtPath<UxmlCodegenSettings>(SettingsPath);

            if (settings != null)
                return settings;

            if (File.Exists(Path.GetFullPath(SettingsPath)))
                AssetDatabase.DeleteAsset(SettingsPath);

            settings = CreateInstance<UxmlCodegenSettings>();

            Directory.CreateDirectory("Assets/Editor");
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }

}
