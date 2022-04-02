using DotNetDevOps.Extensions.EAVFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{
    public class SQLCommand : Command
    {
        public Argument<string> ProjectPath = new Argument<string>("ProjectPath", "The project path to EAV Model Project");
        public Option<string> OutputFile = new Option<string>("OutputFile", "The output sql script for database migrations");

        public SQLCommand() : base("sql", "generalte sql files")
        {
            Add(ProjectPath);
           
            OutputFile.AddAlias("-o");
            OutputFile.SetDefaultValue("dbinit/init.sql");
            Add(OutputFile);

            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
        }
        private async Task Run(ParseResult parseResult, IConsole console)
        {
            var projectPath = parseResult.GetValueForArgument(ProjectPath);
            var outputFile = parseResult.GetValueForOption(OutputFile);
           
            var schema = "$(DBSchema)";
            var model = JToken.Parse(File.ReadAllText(Path.Combine(projectPath, "obj", "manifest.g.json")));
            var models = Directory.Exists(Path.Combine(projectPath, "manifests")) ? Directory.EnumerateFiles(Path.Combine(projectPath, "manifests"))
                .Select(file => JToken.Parse(File.ReadAllText(file)))
                .OrderByDescending(k => Semver.SemVersion.Parse(k.SelectToken("$.version").ToString(),Semver.SemVersionStyles.Strict))
                .ToArray() : Array.Empty<JToken>();

            var optionsBuilder = new DbContextOptionsBuilder<DynamicContext>();
            //  optionsBuilder.UseInMemoryDatabase("test");
            optionsBuilder.UseSqlServer( "dummy", x => x.MigrationsHistoryTable("__MigrationsHistory", schema));
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>();

            var ctx = new DynamicContext(optionsBuilder.Options, Microsoft.Extensions.Options.Options.Create(
                 new DotNetDevOps.Extensions.EAVFramework.DynamicContextOptions
                 {
                     Manifests = new[] { model }.Concat(models).ToArray(),
                     PublisherPrefix = schema,
                     EnableDynamicMigrations = true,
                     Namespace = "KFST.Vanddata.Models",
                    // DTOAssembly = typeof(ApplicationExtensions).Assembly,
                    // DTOBaseClasses = new[] { typeof(BaseOwnerEntity<Model.Identity>), typeof(BaseIdEntity<Model.Identity>), typeof(KeyValueEntity<Model.Identity>) }
                 }),
                 new MigrationManager(NullLogger<MigrationManager>.Instance, Microsoft.Extensions.Options.Options.Create(new MigrationManagerOptions() { SkipValidateSchemaNameForRemoteTypes = true }))
                 , NullLogger<DynamicContext>.Instance);

            // var test = ctx.GetMigrations();

            var migrator = ctx.Database.GetInfrastructure().GetRequiredService<IMigrator>();
            var sql = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
            //  await migrator.MigrateAsync("0"); //Clean up
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
            await File.WriteAllTextAsync(outputFile, sql);

        }
        
    }
}
