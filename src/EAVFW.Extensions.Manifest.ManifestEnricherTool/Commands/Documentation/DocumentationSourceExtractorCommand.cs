using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EAVFW.Extensions.Docs.Extractor;
using Microsoft.Extensions.FileSystemGlobbing;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands.Documentation
{
    /*
     * There are many parameters to the command and generating source for both plugin and wizard requires the tool to be
     * executed twice. Alternatively, the "configuration" could be done using a configuration, which would provide all
     * necessary parameters. Both it would be more rigid and could not easily be changed in a pipeline or other.
     */

    public class DocumentationSourceExtractorCommand : Command
    {
        private readonly IDocumentLogic _documentLogic;

        [Alias("-a")]
        [Alias("--assembly")]
        [Description("Path for the assembly")]
        public FileInfo AssemblyPathOption { get; set; }

        [Alias("-gm")]
        [Alias("--generated-manifest")]
        [Description("Path for the enriched manifest")]
        public FileInfo ManifestPathOption { get; set; }

        [Alias("-p")]
        [Alias("--probing-pattern")]
        [Description("Path pattern used to probe for assemblies, supporting glob patterns")]
        public IEnumerable<string> ProbePathOption { get; set; }

        [Alias("-o")]
        [Alias("--output")]
        [Description("Output directory for genreated documentation source files")]
        public DirectoryInfo OutputOption { get; set; }

        [Alias("-t")]
        [Alias("--target")]
        [Description("What kind of documentation source should be extracted")]
        public Targets Target { get; set; }

        [Alias("-d")]
        [Alias("--debug")]
        [Description("Enable debug output")]
        public bool Debug { get; set; } = false;

        public enum Targets
        {
            Plugins,
            Wizards
        }

        public DocumentationSourceExtractorCommand(IDocumentLogic documentLogic) : base("extract",
            "Extract documentation source")
        {
            _documentLogic = documentLogic ?? throw new ArgumentNullException(nameof(documentLogic));
            Handler = COmmandExtensions.Create(this, Array.Empty<Command>(), Run);
        }

        private async Task<int> Run(ParseResult parseResult, IConsole console)
        {
            if (IsMissingOptions(out var missing))
            {
                Console.WriteLine("The following options are missed: " + string.Join(", ", missing));
                return 126;
            }

            if (!AssemblyPathOption.Exists)
            {
                Console.WriteLine("Assembly does not exists");
                return 1;
            }

            if (ProbePathOption.Count() == 1 && ProbePathOption.First().Contains('*'))
            {
                DebugMsg("No assemblies in list, probing...");

                var probePath = new DirectoryInfo(ProbePathOption.First());
             
                var matcher = new Matcher();
                var basePath = probePath.Parent;
            
                if (probePath.FullName.Contains("**"))
                {
                    basePath = new DirectoryInfo(probePath.FullName.Split("**").First());
                    matcher.AddInclude(probePath.FullName[basePath.FullName.Length..]);
                }
                else if (probePath.Name.Contains("."))
                {
                    matcher.AddInclude(probePath.Name);
                }
            
                ProbePathOption = matcher.GetResultsInFullPath(basePath.FullName).ToList();

            }
            
            DebugMsg($"Found {ProbePathOption.Count()} assemblies");

            if (!ProbePathOption.Any())
            {
                Console.WriteLine("Did not find any assemblies");
                return 1;
            }

            switch (Target)
            {
                case Targets.Plugins:
                    return await GeneratePluginSource();
                case Targets.Wizards:
                    return await GenerateWizardSource();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task<int> GenerateWizardSource()
        {
            if (!ManifestPathOption?.Exists ?? true)
            {
                Console.WriteLine("Manifest does not exists");
                return 1;
            }

            var entityDefinitions = _documentLogic.ExtractWizardDocumentation(
                ManifestPathOption, AssemblyPathOption, ProbePathOption.ToArray());

            var basePath = new DirectoryInfo(CalculateFullPath("wizards"));

            if (!basePath.Exists)
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

        private async Task<int> GeneratePluginSource()
        {
            var plugins = _documentLogic
                .ExtractPluginDocumentation(AssemblyPathOption, ProbePathOption.ToArray())
                .ToArray();

            var jsonString = JsonSerializer.Serialize(plugins, new JsonSerializerOptions
            {
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

            if (ProbePathOption == null)
                missing.Add(nameof(ProbePathOption));

            return false;
        }

        private void DebugMsg(string msg)
        {
            if (Debug)
                Console.WriteLine(msg);
        }
    }
}
