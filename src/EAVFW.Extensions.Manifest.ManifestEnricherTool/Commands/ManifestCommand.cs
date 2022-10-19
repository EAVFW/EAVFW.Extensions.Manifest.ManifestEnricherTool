using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{
    public class ManifestCommand : Command
    {
        private readonly ILogger<ManifestCommand> ilogger;

        public ManifestCommand(
            ILogger<ManifestCommand> ilogger ,
            ILogger<ManifestNewMigrationCommand> logger,
            ILogger<ManifestFixMigrationCommand> fixLogger) : base("manifest", "Work with the manifest")
        {
            this.AddCommand(new ManifestNewMigrationCommand(logger));
            this.AddCommand(new ManifestFixMigrationCommand(fixLogger));
            this.ilogger = ilogger;

            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
        }
            
        private async Task Run(ParseResult parseResult, IConsole console)
        {
            ilogger.LogInformation("Done");
        }
    }
}
