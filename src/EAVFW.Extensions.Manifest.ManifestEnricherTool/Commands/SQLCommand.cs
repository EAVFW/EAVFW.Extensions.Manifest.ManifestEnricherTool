using EAVFramework;
using EAVFW.Extensions.Manifest.SDK;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{


     public class SQLApplyCommand : Command
    {
        public Option<string> ClientSecret = new Option<string>("ClientSecret", "");
        public Option<string> ClientId = new Option<string>("ClientId", "");
        public Option<string> Server = new Option<string>("Server", "");
        public Option<string> DatabaseName = new Option<string>("DatabaseName", "");

        public Option<string[]> Replacements = new Option<string[]>("Values");
        public Option<string[]> Files = new Option<string[]>("Files");

        public SQLApplyCommand() : base("apply")
        {
            Server.AddAlias("-S");
            Add(Server);

            ClientSecret.AddAlias("--client-secret");
            Add(ClientSecret);

            ClientId.AddAlias("--client-id");
            Add(ClientId);
            DatabaseName.AddAlias("-d");
            Add(DatabaseName);

            Replacements.AddAlias("-v");
            Add(Replacements);


            Replacements.AllowMultipleArgumentsPerToken = true;

            Files.AddAlias("-i");
            Add(Files);


            Files.AllowMultipleArgumentsPerToken = true;


            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
        }

        private async Task Run(ParseResult arg1, IConsole arg2)
        {
            var clientid = arg1.GetValueForOption(ClientId);
            var clientSecret = arg1.GetValueForOption(ClientSecret);
            var server = arg1.GetValueForOption(Server);
            var database = arg1.GetValueForOption(DatabaseName);

            var files = arg1.GetValueForOption(Files);
            var _replacements = arg1.GetValueForOption(Replacements);

            // Use your own server, database, app ID, and secret.
            string ConnectionString = $@"Server={server}; Authentication=Active Directory Service Principal;Command Timeout=300; Encrypt=True; Database={database}; User Id={clientid}; Password={clientSecret}";
            //var files = new[] { @"C:\dev\MedlemsCentralen\obj\dbinit\init.sql", @"C:\dev\MedlemsCentralen\obj\dbinit\init-systemadmin.sql" };
            var replacements = _replacements.ToDictionary(k => k.Substring(0, k.IndexOf('=')), v => v.Substring(v.IndexOf('=') + 1));

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
      
                await conn.OpenAsync();
                foreach(var file in files)
                {
                    var cmdText = File.ReadAllText(file);
                    foreach(var r in replacements)
                    {
                        cmdText = cmdText.Replace($"$({r.Key})", r.Value);
                    }


                   

                    foreach (var sql in cmdText.Split("GO"))
                    {
                        using var cmd = conn.CreateCommand();
                      
                        cmd.CommandText = sql.Trim();
                        //  await context.Context.Database.ExecuteSqlRawAsync(sql);

                        if (!string.IsNullOrEmpty(cmd.CommandText))
                        {
                            var r = await cmd.ExecuteNonQueryAsync();
                            Console.WriteLine("Rows changed: " + r);
                        }
                    }




                  
                     
                }
              
            }
        }
    }
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
            var schema = "${schema}";
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
                 new EAVFramework.DynamicContextOptions
                 {
                     Manifests = new[] { model }.Concat(models).ToArray(),
                     PublisherPrefix = schema,
                     EnableDynamicMigrations = true,
                     Namespace = "EAVFW.Extensions.Manifest",
                    // DTOAssembly = typeof(ApplicationExtensions).Assembly,
                    // DTOBaseClasses = new[] { typeof(BaseOwnerEntity<Model.Identity>), typeof(BaseIdEntity<Model.Identity>), typeof(KeyValueEntity<Model.Identity>) }
                 }),
                 new MigrationManager(NullLogger<MigrationManager>.Instance, Microsoft.Extensions.Options.Options.Create(new MigrationManagerOptions() { SkipValidateSchemaNameForRemoteTypes = true }))
                 , NullLogger<DynamicContext>.Instance);

 

            var migrator = ctx.Database.GetInfrastructure().GetRequiredService<IMigrator>();
            var sql = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
 
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(outputFile, sql);

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
