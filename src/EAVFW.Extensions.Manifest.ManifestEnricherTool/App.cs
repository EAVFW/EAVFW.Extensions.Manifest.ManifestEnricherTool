


// See https://aka.ms/new-console-template for more information
using EAVFW.Extensions.Manifest.ManifestEnricherTool;
using EAVFW.Extensions.Manifest.SDK;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

public class App : System.CommandLine.RootCommand
{
    public Option<string> Path = new Option<string>("--path", "The path");
    public Option<string> Prefix = new Option<string>(new string[] { "--customizationprefix" }, "The prefix");

    private readonly ILogger<App> logger;
    private readonly IManifestEnricher manifestEnricher;

    public App(ILogger<App> logger, IEnumerable<Command> commands, IManifestEnricher manifestEnricher) : base($"Generating Manifest: v{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}")
    {
        this.logger=logger;
        this.manifestEnricher = manifestEnricher;
        Path.IsRequired=true;
       
        Add(Path); 
        Add(Prefix);

        foreach (var command in commands)
            Add(command);

        Handler = CommandHandler.Create<ParseResult,IConsole>(Run);
    }

    public async Task Run(ParseResult parseResult, IConsole console) //(string path, string customizationprefix)
    {
        var path = parseResult.GetValueForOption(Path);
        var customizationprefix = parseResult.GetValueForOption(Prefix);

        console.Out.Write($"Generating Manifest: v{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");

       // var cmd = new EAVFW.Extensions.Manifest.ManifestEnricherTool.RootCommand();

      


        using (var fs = File.OpenRead(path))
        {

            var jsonraw = Newtonsoft.Json.Linq.JToken.ReadFrom(new Newtonsoft.Json.JsonTextReader(new StreamReader(fs))) as JObject;
            var others = Directory.GetFiles(System.IO.Path.GetDirectoryName(path), "manifest.*.json")
          .Where(c => !string.Equals("manifest.schema.json", System.IO.Path.GetFileName(c), System.StringComparison.OrdinalIgnoreCase));
            foreach (var other in others)
            {
                jsonraw.Merge(JToken.Parse(File.ReadAllText(other)), new JsonMergeSettings
                {
                    // union array values together to avoid duplicates
                    MergeArrayHandling = MergeArrayHandling.Union,
                    PropertyNameComparison = System.StringComparison.OrdinalIgnoreCase,
                    MergeNullValueHandling = MergeNullValueHandling.Ignore
                });
            }

            JsonDocument json = await manifestEnricher.LoadJsonDocumentAsync(jsonraw, customizationprefix, logger);
        }



    }
}