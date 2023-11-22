using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EAVFW.Extensions.Docs.Extractor;
using EAVFW.Extensions.Docs.Generator;
using EAVFW.Extensions.Manifest.SDK;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands.Documentation
{
    public class DocumentationGeneratorCommand : Command
    {
        [Alias("-gm")]
        [Description("Path to the generate manifest")]
        public FileInfo GeneratedManifestPath { get; set; }

        [Alias("-p")]
        [Description("Path to plugin source")]
        public FileInfo PluginSourcePath { get; set; }

        [Alias("-w")]
        [Description("Path to wizard source directory")]
        public DirectoryInfo WizardSourcePath { get; set; }

        [Alias("-c")]
        [Description("Component")]
        public string Component { get; set; }

        [Alias("-o")] [Description("Output")] public FileInfo Output { get; set; }

        public DocumentationGeneratorCommand() : base("generate", "Generate")
        {
            Handler = COmmandExtensions.Create(this, Array.Empty<Command>(), Run);
        }

        private async Task<int> Run(ParseResult parseResult, IConsole console)
        {
            if (IsFilesAndFoldersMissing(out var missing))
            {
                console.WriteLine($"The following file(s)/folder(s) are missing: {string.Join(", ", missing)}");
                return 1;
            }

            var documentationGenerator = new ReadMeDocumentationGenerator();

            await using var pluginStream = PluginSourcePath.OpenRead();
            documentationGenerator.AddPluginSource(
                await JsonSerializer.DeserializeAsync<IEnumerable<PluginDocumentation>>(pluginStream));


            var wizards = new Dictionary<string, EntityDefinition>();
            var files = Directory.GetFiles(WizardSourcePath.FullName, "*.json");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                await using var openStream = fileInfo.OpenRead();
                var w = await JsonSerializer.DeserializeAsync<EntityDefinition>(openStream);
                wizards[Path.GetFileNameWithoutExtension(fileInfo.Name)] = w;
            }

            documentationGenerator.AddWizardSource(wizards);

            await using var manifestStream = GeneratedManifestPath.OpenRead();
            documentationGenerator.AddGeneratedManifest(
                await JsonSerializer.DeserializeAsync<ManifestDefinition>(manifestStream));

            await documentationGenerator.Write(Output, Component);

            return 0;
        }

        private bool IsFilesAndFoldersMissing(out List<string> missing)
        {
            missing = new List<string>();

            if (!GeneratedManifestPath.Exists)
                missing.Add(GeneratedManifestPath.Name);

            if (!PluginSourcePath.Exists)
                missing.Add(PluginSourcePath.Name);

            if (!WizardSourcePath.Exists)
                missing.Add(WizardSourcePath.Name);

            return missing.Count > 0;
        }
    }
}
