using EAVFramework;
using EAVFW.Extensions.Manifest.SDK;
using EAVFW.Extensions.Manifest.SDK.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Sprache;
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
        private readonly SQLMigrationGenerator generator;
        public Argument<string> ProjectPath = new Argument<string>("ProjectPath", "The project path to EAV Model Project");
        public Option<string> OutputFile = new Option<string>("OutputFile", "The output sql script for database migrations");

        public Option<bool> ShouldGeneratePermissions = new Option<bool>("GeneratePermissions", "Should permissions be generated for each entity");
        public Option<string> SystemUserEntity = new Option<string>("SystemUserEntity", "The system user entity used to popuplate a system administrator account");
       

        public SQLCommand(SQLMigrationGenerator generator) : base("sql", "generalte sql files")
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
            
            Add(new SQLApplyCommand());
           
            this.generator = generator;
        }
        private async Task Run(ParseResult parseResult, IConsole console)
        {
            var projectPath = parseResult.GetValueForArgument(ProjectPath);
            var outputFile = parseResult.GetValueForOption(OutputFile);
            var outputDirectory = Path.GetDirectoryName(outputFile);

          
            var result = await generator.GenerateSQL(projectPath, parseResult.GetValueForOption(ShouldGeneratePermissions),
                parseResult.GetValueForOption(SystemUserEntity),(builder) => { builder.UseNetTopologySuite(); });
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(outputFile, result.SQL);
            console.WriteLine("Written: " + Path.GetFullPath(outputFile));

            if (result.Permissions != null)
            {
                await File.WriteAllTextAsync($"{outputDirectory}/init-systemadmin.sql", result.Permissions);
            }
                
        }

    
       
    }
}
