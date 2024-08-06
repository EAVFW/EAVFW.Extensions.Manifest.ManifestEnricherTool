using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace EAVFW.Extensions.Docs.Extractor
{
    public static class XmlDocumentationHelper
    {
        private static readonly Dictionary<string, string?> LoadedXmlDocumentation = new();
        private static readonly HashSet<Assembly> LoadedAssemblies = new();

        public static string? GetDocumentation(this Type type)
        {
            LoadXmlDocumentation(type.Assembly);

            var key = $"T:{type.FullName}";
            LoadedXmlDocumentation.TryGetValue(key, out var docs);
            return docs;
        }

        private static void LoadXmlDocumentation(Assembly assembly)
        {
            if (LoadedAssemblies.Contains(assembly))
                return; // Already loaded

            var directoryPath = Path.GetDirectoryName(assembly.Location);
            if (string.IsNullOrEmpty(directoryPath))
                return;

            var xmlFilePath = Path.Combine(directoryPath, assembly.GetName().Name + ".xml");

            if (!File.Exists(xmlFilePath)) return;

            using var xmlReader = XmlReader.Create(new StringReader(File.ReadAllText(xmlFilePath)));


            var started = false;
            var name = "";
            while (xmlReader.Read())
            {
                if (xmlReader.Name == "member")
                {
                    started = true;
                    name = xmlReader.GetAttribute("name");
                    continue;
                }

                if (xmlReader is { Name: "member", NodeType: XmlNodeType.EndElement } && started)
                {
                    name = "";
                    started = false;
                    continue;
                }

                if (xmlReader.Name == "summary" && !string.IsNullOrWhiteSpace(name))
                    LoadedXmlDocumentation[name] = xmlReader.ReadInnerXml();
            }

            LoadedAssemblies.Add(assembly);
        }
    }
}
