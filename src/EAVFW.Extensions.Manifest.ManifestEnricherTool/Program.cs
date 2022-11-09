


// See https://aka.ms/new-console-template for more information
using EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands;
using EAVFW.Extensions.Manifest.SDK;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool
{
    public static class Program
    {

        static ServiceCollection ConfigureServices(ServiceCollection serviceCollection)
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

            serviceCollection.AddSingleton<Command, InstallCommand>();
            serviceCollection.AddSingleton<Command, SQLCommand>();
            serviceCollection.AddSingleton<Command, ManifestCommand>();
            serviceCollection.AddSingleton<Command, CertCommand>();
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

