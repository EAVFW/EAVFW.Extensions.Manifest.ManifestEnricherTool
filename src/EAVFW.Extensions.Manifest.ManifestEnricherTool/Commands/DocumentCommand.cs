using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EAVFW.Extensions.Docs.Extracter;
using EAVFW.Extensions.Docs.Generator;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{
    public class DocumentCommand : Command
    {
        private readonly IDocumentLogic documentLogic;

        [Alias("-a")]
        [Alias("--assembly")]
        [Description("Path for the assembly")]
        public FileInfo AssemblyPathOption { get; set; }

        [Alias("-m")]
        [Alias("--manifest")]
        [Description("Path for the enriched manifest")]
        public FileInfo ManifestPathOption { get; set; }

        [Alias("-p")]
        [Alias("--probing-path")]
        [Description("Path to probe for dependent assemblies")]
        public DirectoryInfo RootPathOption { get; set; }

        [Alias("-c")]
        [Alias("--configuration")]
        [Description("Configuration for the built assembly")]
        public string ConfigurationOption { get; set; }

        [Alias("--component")]
        [Description("Component to generate documentation. E.g., `mainfest.component.json`")]
        public string ComponentOption { get; set; }

        [Alias("-f")]
        [Alias("--framework")]
        [Description("Framework confugraiton for the built assembly")]
        public string FrameworkOption { get; set; }

        [Alias("-o")]
        [Alias("--output")]
        [Description("Output directory for genreated documentation source files")]
        public DirectoryInfo OutputOption { get; set; }

        [Alias("-t")]
        [Alias("--target")]
        [Description("Target?")]
        public Targets Target { get; set; }

        public enum Targets
        {
            Plugins,
            Wizards
        }

        public DocumentCommand(IDocumentLogic documentLogic) : base("docs", "Generate documentation")
        {
            this.documentLogic = documentLogic ?? throw new ArgumentNullException(nameof(documentLogic));
            Handler = COmmandExtensions.Create(this, Array.Empty<Command>(), Run);
        }

        private async Task<int> Run(ParseResult parseResult, IConsole console)
        {
            if (IsMissingOptions(out var missing))
            {
                Console.WriteLine("The following options are missed: " + string.Join(", ", missing));
                return 126;
            }

            if (!RootPathOption.Exists)
            {
                Console.WriteLine("Probing path does not exists");
                return 1;
            }

            if (!AssemblyPathOption.Exists)
            {
                Console.WriteLine("Assembly does not exists");
                return 1;
            }

            switch (Target)
            {
                case Targets.Plugins:
                    return await HandlePlugins();
                case Targets.Wizards:
                    return await HandleWizards();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task<int> HandleWizards()
        {
            if (!ManifestPathOption?.Exists ?? true)
            {
                Console.WriteLine("Manifest does not exists");
                return 1;
            }

            var entityDefinitions = documentLogic.ExtractWizardDocumentation(
                ManifestPathOption,
                new PluginInfo(RootPathOption,
                    AssemblyPathOption,
                    ConfigurationOption,
                    FrameworkOption));

            var basePath = new DirectoryInfo(CalculateFullPath("wizards"));

            if(!basePath.Exists)
                basePath.Create();
            
            foreach (var (key, value) in entityDefinitions)
            {
                var fileName = Path.Combine(basePath.FullName, $"{key}.json");

                await using var createStream = File.Create(fileName);
                await JsonSerializer.SerializeAsync(createStream, value, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await createStream.DisposeAsync();
            }

            return 0;
        }

        private async Task<int> HandlePlugins()
        {
            var plugins = documentLogic
                .ExtractPluginDocumentation(new PluginInfo(RootPathOption, AssemblyPathOption, ConfigurationOption,
                    FrameworkOption))
                .ToArray();

            var jsonString = JsonSerializer.Serialize(plugins, new JsonSerializerOptions
            {
                Converters = { new PluginRegistrationAttributeConverter() },
                WriteIndented = true
            });

            var path = CalculateFullPath("plugins.json");
            await File.WriteAllTextAsync(path, jsonString);

            return 0;
        }

        private string CalculateFullPath(string path)
        {
            if (OutputOption != null)
            {
                path = Path.Combine(OutputOption.FullName, path);
            }

            return path;
        }

        private bool IsMissingOptions(out List<string> missing)
        {
            missing = new List<string>();

            if (AssemblyPathOption == null)
                missing.Add(nameof(AssemblyPathOption));

            if (RootPathOption == null)
                missing.Add(nameof(RootPathOption));

            if (string.IsNullOrWhiteSpace(ConfigurationOption))
                missing.Add(nameof(ConfigurationOption));

            if (string.IsNullOrWhiteSpace(FrameworkOption))
                missing.Add(nameof(FrameworkOption));

            return false;
        }
    }
}
