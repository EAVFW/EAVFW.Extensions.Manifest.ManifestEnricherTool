using LibGit2Sharp;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Semver;
using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{

    public class ManifestFixMigrationCommand : Command
    {
        public Option<string> ProjectPath = new Option<string>("--project-path", "The project path to EAV Model Project");
        public Option<string> VersionToFix = new Option<string>("--migration-version", "The version to fix");
        public Option<string> ConnectionString = new Option<string>("--connection-string", "The db connectionstring");
        public Option<string> DatabaseName = new Option<string>("--database", "");
        public Option<string> SchemaName = new Option<string>("--schema", "");
        public Option<string> Prefix = new Option<string>("--prefix", "");

        private readonly ILogger<ManifestFixMigrationCommand> logger;

        public ManifestFixMigrationCommand(ILogger<ManifestFixMigrationCommand> logger) : base("fix-migration", "Fix migration")
        {
            this.logger = logger;

           

            ProjectPath.SetDefaultValue(".");
            Add(ProjectPath);

            VersionToFix.AddAlias("-v");
           
            Add(VersionToFix);
            Add(DatabaseName);
            Add(ConnectionString);
            Add(Prefix);

            Add(SchemaName);

            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
        }
        private async Task Run(ParseResult parseResult, IConsole console)
        {

            var gitfolder = ProjectPath.GetValue(parseResult);
            while (!Directory.Exists(Path.Combine(gitfolder,".git")))
            {
                gitfolder = Directory.GetParent(gitfolder).FullName;
            }

            using var repo = new Repository(gitfolder);

            if(repo.Diff.Compare<TreeChanges>(new[] { "*manifest*.json" }).Any())
            {

                logger.LogWarning("Please commit or stash your current changes.");
                return;
            }

            //TODO run the generate command first.

            var files = Directory.GetFiles(ProjectPath.GetValue(parseResult), "*manifest.json", SearchOption.AllDirectories)
                .Where(f=>Path.GetFileName(f).ToLower() == "manifest.json")
                .Where(f=>!f.Replace("\\","/").Contains("/bin/"));

            if (!files.Any() || files.Skip(1).Any())
            {
                logger.LogWarning("The generated Manifest was not found: {Files}", String.Join(",",files));
                return;

            }

            var versionToFix = Semver.SemVersion.Parse(VersionToFix.GetValue(parseResult), Semver.SemVersionStyles.Strict);
            var file = files.Single();
          
           
                
                var manifest = JToken.Parse(File.ReadAllText(file));
                var manifestVersion = Semver.SemVersion.Parse(manifest.SelectToken("$.version")?.ToString(), Semver.SemVersionStyles.Strict);
                var manifestNewVersion = manifestVersion.WithPatch(manifestVersion.Patch + 1);
                manifest.SelectToken("$.version").Replace(manifestVersion.WithPatch(manifestVersion.Patch + 1).ToString());
            
                File.WriteAllText(file, manifest.ToString());
            

           
                var fileToChange = $"{Path.GetDirectoryName(file)}/manifests/manifest.{versionToFix}.g.json";
                var newVersion = versionToFix.WithPatch(versionToFix.Patch + 1);
                var fileToChangeNewPath = $"{Path.GetDirectoryName(file)}/manifests/manifest.{newVersion}.g.json";
          
            
                File.Move(fileToChange, fileToChangeNewPath);
                var t = JToken.Parse(File.ReadAllText(fileToChangeNewPath));
                t.SelectToken("$.version").Replace(newVersion.ToString());
                File.WriteAllText(fileToChangeNewPath, t.ToString());
           
            var connectionstring = ConnectionString.GetValue(parseResult) ?? $"Server=127.0.0.1; Initial Catalog={DatabaseName.GetValue(parseResult)}; User ID=sa; Password=Bigs3cRet; TrustServerCertificate=True";
            using (SqlConnection conn = new SqlConnection(connectionstring))
            {
               await conn.OpenAsync();

                using var cmd1 = conn.CreateCommand();
                cmd1.CommandText = $"UPDATE [{SchemaName.GetValue(parseResult)}].[__MigrationsHistory] SET MigrationId = '{migrationName(manifestNewVersion)}' WHERE MigrationId = '{migrationName(manifestVersion)}'";

                var changed1 = await cmd1.ExecuteNonQueryAsync();
                console.WriteLine("Row CHanged: " + changed1);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"UPDATE [{SchemaName.GetValue(parseResult)}].[__MigrationsHistory] SET MigrationId = '{migrationName(newVersion)}' WHERE MigrationId = '{migrationName(versionToFix)}'";

               var changed= await cmd.ExecuteNonQueryAsync();
                console.WriteLine("Row CHanged: "+ changed);


            }

            string migrationName(SemVersion version)
            {
                return $"{Prefix.GetValue(parseResult)}_{version.ToString().Replace(".", "_")}";
            }

                logger.LogInformation("Done");
        }
    }
}
