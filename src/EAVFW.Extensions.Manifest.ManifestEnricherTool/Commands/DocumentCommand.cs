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

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{
    public class DocumentCommand : Command
    {
        private readonly IDocumentLogic documentLogic;


        [Alias("-a")]
        [Alias("--assembly")]
        [Description("Path for the assembly")]
        public FileInfo AssemblyPathOption { get; set; }

        [Alias("-p")]
        [Alias("--probing-path")]
        [Description("Path to probe for dependent assemblies")]
        public DirectoryInfo RootPathOption { get; set; }

        [Alias("-c")]
        [Alias("--configuration")]
        [Description("Configuration for the built assembly")]
        public string ConfigurationOption { get; set; }

        [Alias("-f")]
        [Alias("--framework")]
        [Description("Framework confugraiton for the built assembly")]
        public string FrameworkOption { get; set; }


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

            var plugins = documentLogic
                .ExtractPluginDocumentation(new PluginInfo(RootPathOption, AssemblyPathOption, ConfigurationOption, FrameworkOption))
                .ToArray();

            var jsonString = JsonSerializer.Serialize(plugins, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync("docs.json", jsonString);
            
            var groups = plugins.GroupBy(x => x.Entity!.Name);

            Console.WriteLine("# Plugins ");
            foreach (var group in groups)
            {
                Console.WriteLine($"## {group.FirstOrDefault()?.Entity?.Name}");
                foreach (var pluginDocumentation in group)
                {
                    Console.WriteLine(pluginDocumentation.ToString());
                }
            }


            return 0;
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
