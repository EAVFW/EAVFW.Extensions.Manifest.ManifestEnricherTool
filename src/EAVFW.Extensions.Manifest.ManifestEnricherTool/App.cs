


// See https://aka.ms/new-console-template for more information
using EAVFW.Extensions.Manifest.ManifestEnricherTool;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Reflection;
using System.Text.Json;

public class App : RootCommand
{
    public Option<string> Path = new Option<string>("--path", "The path");
    public Option<string> Prefix = new Option<string>(new string[] { "--customizationprefix" }, "The prefix");

    private readonly ILogger<App> logger;



    public App(ILogger<App> logger) : base($"Generating Manifest: v{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}")
    {
        this.logger=logger;
        Add(Path);
        Add(Prefix);

        Handler = CommandHandler.Create(Run);
    }

    public async Task Run(ParseResult parseResult, IConsole console) //(string path, string customizationprefix)
    {
        var path = parseResult.GetValueForOption(Path);
        var customizationprefix = parseResult.GetValueForOption(Prefix);

        console.Out.Write($"Generating Manifest: v{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");

        var cmd = new ManifestCommand();

        using (var fs = File.OpenRead(path))
        {


            JsonDocument json = await cmd.LoadJsonDocumentAsync(fs, customizationprefix, logger);
        }



    }
}