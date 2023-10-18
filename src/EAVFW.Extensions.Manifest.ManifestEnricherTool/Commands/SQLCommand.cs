using EAVFramework;
using EAVFW.Extensions.Manifest.SDK;
using Microsoft.Azure.Documents.Spatial;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Point = NetTopologySuite.Geometries.Point;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{
    public class SQLCommand : Command
    {
       

        public Argument<string> ProjectPath = new Argument<string>("ProjectPath", "The project path to EAV Model Project");
        public Option<string> OutputFile = new Option<string>("OutputFile", "The output sql script for database migrations");

        public Option<bool> ShouldGeneratePermissions = new Option<bool>("GeneratePermissions", "Should permissions be generated for each entity");
        public Option<string> SystemUserEntity = new Option<string>("SystemUserEntity", "The system user entity used to popuplate a system administrator account");
        private readonly IManifestPermissionGenerator manifestPermissionGenerator;

        public SQLCommand(IManifestPermissionGenerator manifestPermissionGenerator) : base("sql", "generalte sql files")
        {
            ProjectPath.SetDefaultValue(".");
            Add(ProjectPath);
         

            OutputFile.AddAlias("-o");
            OutputFile.SetDefaultValue("obj/dbinit/init.sql");
            Add(OutputFile);


            ShouldGeneratePermissions.SetDefaultValue(true);
            Add(ShouldGeneratePermissions);

            SystemUserEntity.SetDefaultValue("SystemUsers");
            Add(SystemUserEntity);

            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
            this.manifestPermissionGenerator = manifestPermissionGenerator ?? throw new ArgumentNullException(nameof(manifestPermissionGenerator));

            Add(new SQLApplyCommand());
            
        }
        private async Task Run(ParseResult parseResult, IConsole console)
        {
            var projectPath = parseResult.GetValueForArgument(ProjectPath);
            var outputFile = parseResult.GetValueForOption(OutputFile);
            var outputDirectory = Path.GetDirectoryName(outputFile);
            var schema = "$(DBSchema)";
            var model = JToken.Parse(File.ReadAllText(Path.Combine(projectPath, "obj", "manifest.g.json")));
            var models = Directory.Exists(Path.Combine(projectPath, "manifests")) ? Directory.EnumerateFiles(Path.Combine(projectPath, "manifests"))
                .Select(file => JToken.Parse(File.ReadAllText(file)))
                .OrderByDescending(k => Semver.SemVersion.Parse(k.SelectToken("$.version").ToString(),Semver.SemVersionStyles.Strict))
                .ToArray() : Array.Empty<JToken>();

            var optionsBuilder = new DbContextOptionsBuilder<DynamicContext>();
            //  optionsBuilder.UseInMemoryDatabase("test");
            optionsBuilder.UseSqlServer( "dummy", x => x.MigrationsHistoryTable("__MigrationsHistory", schema).UseNetTopologySuite());
            optionsBuilder.EnableSensitiveDataLogging();
            
            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.ReplaceService<IMigrationsAssembly, DbSchemaAwareMigrationAssembly>();
           
            var ctx = new DynamicContext(optionsBuilder.Options, Microsoft.Extensions.Options.Options.Create(
                 new EAVFramework.DynamicContextOptions
                 {
                     Manifests = new[] { model }.Concat(models).ToArray(),
                     Schema = schema,
                     EnableDynamicMigrations = true,
                     Namespace = "EAVFW.Extensions.Manifest",
                    // DTOAssembly = typeof(ApplicationExtensions).Assembly,
                    // DTOBaseClasses = new[] { typeof(BaseOwnerEntity<Model.Identity>), typeof(BaseIdEntity<Model.Identity>), typeof(KeyValueEntity<Model.Identity>) }
                 }),
                 new MigrationManager(NullLogger<MigrationManager>.Instance, Microsoft.Extensions.Options.Options.Create(new MigrationManagerOptions() {
                     SkipValidateSchemaNameForRemoteTypes = true, Schema = schema, Namespace = "EAVFW.Extensions.Manifest", 
                 }),new DynamicCodeServiceFactory())
                 , NullLogger<DynamicContext>.Instance);

 

            var migrator = ctx.Database.GetInfrastructure().GetRequiredService<IMigrator>();
            var sql = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
 
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(outputFile, sql);
            console.WriteLine("Written: " + Path.GetFullPath(  outputFile));
            
            if(parseResult.GetValueForOption(ShouldGeneratePermissions))
                await InitializeSystemAdministrator(parseResult, outputDirectory,model);
         }

       
        public async Task InitializeSystemAdministrator(ParseResult parseResult, string outputDirectory, JToken model)
        {
            var systemUserEntity = parseResult.GetValueForOption(SystemUserEntity);
            //TODO : Fix such this is only done if security package is installed.

            var sb = await manifestPermissionGenerator.CreateInitializationScript(model, systemUserEntity);


            await File.WriteAllTextAsync($"{outputDirectory}/init-systemadmin.sql", sb);
        }
    
    }
}
