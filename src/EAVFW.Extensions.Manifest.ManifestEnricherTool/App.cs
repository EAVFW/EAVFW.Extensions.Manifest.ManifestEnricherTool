// See https://aka.ms/new-console-template for more information

using System;
using EAVFW.Extensions.Manifest.ManifestEnricherTool;
using EAVFW.Extensions.Manifest.SDK;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

public class App : System.CommandLine.RootCommand
{
    public Option<FileInfo> Path = new Option<FileInfo>("--path", "The path");
    public Option<string> Prefix = new Option<string>(new string[] { "--customizationprefix" }, "The prefix");

    private readonly ILogger<App> logger;
    private readonly IManifestEnricher manifestEnricher;
    private readonly IManifestMerger _manifestMerger;

    public App(ILogger<App> logger, IEnumerable<Command> commands, IManifestEnricher manifestEnricher, IManifestMerger manifestMerger) : base(
        $"Generating Manifest: v{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}")
    {
        this.logger = logger;
        this.manifestEnricher = manifestEnricher;
        _manifestMerger = manifestMerger ?? throw new ArgumentNullException(nameof(manifestMerger));
        Path.IsRequired = true;

        Add(Path);
        Add(Prefix);

        foreach (var command in commands)
            Add(command);

        Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
    }

    public async Task Run(ParseResult parseResult, IConsole console)
    {
        var path = parseResult.GetValueForOption(Path);
        var customizationprefix = parseResult.GetValueForOption(Prefix);

        console.Out.Write(
            $"Generating Manifest: v{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion} - {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyVersionAttribute>()?.Version}");

        var t = await _manifestMerger.MergeManifests(path);
        JsonDocument json = await manifestEnricher.LoadJsonDocumentAsync(t, customizationprefix, logger);
    }
}
