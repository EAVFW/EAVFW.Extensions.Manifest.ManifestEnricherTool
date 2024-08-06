
// See https://aka.ms/new-console-template for more information
using EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands;
using EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands.GPT;
using EAVFW.Extensions.Manifest.SDK;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Threading.Tasks;
using EAVFW.Extensions.Docs.Extractor;
using EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands.Documentation;
using EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands.Gzip;
using EAVFW.Extensions.Manifest.SDK.Migrations;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool
{
    public static class Program
    {

        static IServiceCollection ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection
               .AddLogging(configure =>
               {
                   configure.SetMinimumLevel(LogLevel.Debug);
                   configure.AddDebug();
                   configure.AddConsole();
               })
               .AddManifestSDK<SQLClientParameterGenerator>()              
               .AddSingleton<App>();
            serviceCollection.AddScoped<SQLMigrationGenerator>();

            serviceCollection.AddDocument();

            serviceCollection.AddSingleton<Command, InstallCommand>();
            serviceCollection.AddSingleton<Command, SQLCommand>();
            serviceCollection.AddSingleton<Command, ManifestCommand>();
            serviceCollection.AddSingleton<Command, CertCommand>();
            serviceCollection.AddSingleton<Command, GzipCommand>();
            serviceCollection.AddSingleton<Command, DocumentationSourceCommand>();
            serviceCollection.AddGPT();

            serviceCollection.AddTransient<IManifestMerger, ManifestMerger>();
            serviceCollection.AddTransient<IModuleMetadataEnricher, ModuleMetadataEnricher >();
            
            serviceCollection.AddHttpClient();
            return serviceCollection;
        }

        public static async Task<int> Main(string[] args)
        {

            using var services = ConfigureServices(new ServiceCollection()).BuildServiceProvider();

            var result = await services.GetRequiredService<App>().InvokeAsync(args);

            return result;
        }

    }

}

