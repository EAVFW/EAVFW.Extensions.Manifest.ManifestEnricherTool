


// See https://aka.ms/new-console-template for more information
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
               .AddSingleton<App>();

            return serviceCollection;
        }

        public static async Task<int> Main(string[] args)
        {

            var services = ConfigureServices(new ServiceCollection()).BuildServiceProvider();

            return await services.GetRequiredService<App>().InvokeAsync(args);
        }

    }

}

