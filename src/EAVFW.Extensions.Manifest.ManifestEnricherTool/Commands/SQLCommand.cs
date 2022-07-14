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
using System.Text;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{
    public class SQLCommand : Command
    {
        public Argument<string> ProjectPath = new Argument<string>("ProjectPath", "The project path to EAV Model Project");
        public Option<string> OutputFile = new Option<string>("OutputFile", "The output sql script for database migrations");

        public SQLCommand() : base("sql", "generalte sql files")
        {
            ProjectPath.SetDefaultValue(".");
            Add(ProjectPath);
         

            OutputFile.AddAlias("-o");
            OutputFile.SetDefaultValue("obj/dbinit/init.sql");
            Add(OutputFile);

            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
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

 

            var migrator = ctx.Database.GetInfrastructure().GetRequiredService<IMigrator>();
            var sql = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
 
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(outputFile, sql);

            await InitializeSystemAdministrator(outputDirectory,model);
         }

       
        public async Task InitializeSystemAdministrator(string outputDirectory, JToken model)
        {
            
            //TODO : Fix such this is only done if security package is installed.
            
            var sb = new StringBuilder();
            var adminSGId = "$(SystemAdminSecurityGroupId)";
            sb.AppendLine("DECLARE @adminSRId uniqueidentifier");
            sb.AppendLine("DECLARE @permissionId uniqueidentifier");
            sb.AppendLine($"SET @adminSRId = ISNULL((SELECT s.Id   FROM [$(DBName)].[$(DBSchema)].[SecurityRoles] s WHERE s.Name = 'System Administrator'),'{Guid.NewGuid()}')");
            sb.AppendLine($"IF NOT EXISTS(SELECT * FROM [$(DBName)].[$(DBSchema)].[Identities] WHERE [Id] = '{adminSGId}')");
            sb.AppendLine("BEGIN");
            sb.AppendLine($"INSERT INTO [$(DBName)].[$(DBSchema)].[Identities] (Id, Name, ModifiedOn,CreatedOn,CreatedById,ModifiedById,OwnerId) VALUES('{adminSGId}', 'System Administrator Group', CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'{adminSGId}','{adminSGId}','{adminSGId}')");
            sb.AppendLine($"INSERT INTO [$(DBName)].[$(DBSchema)].[SecurityGroups] (Id) VALUES('{adminSGId}')");
            sb.AppendLine($"INSERT INTO [$(DBName)].[$(DBSchema)].[Identities] (Id, Name,ModifiedOn,CreatedOn,CreatedById,ModifiedById,OwnerId) VALUES ('$(UserGuid)', '$(UserName)', CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'{adminSGId}','{adminSGId}','{adminSGId}')");
            sb.AppendLine($"INSERT INTO [$(DBName)].[$(DBSchema)].[Users] (Id,Email) VALUES ('$(UserGuid)', '$(UserEmail)');");
            //sb.AppendLine($"INSERT INTO [$(DBName)].[$(DBSchema)].[Identities] (Id, Name,ModifiedOn,CreatedOn,CreatedById,ModifiedById,OwnerId) VALUES ('$(UserGuid)', '$(UserName)', CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'{adminSGId}','{adminSGId}','{adminSGId}')");
            //sb.AppendLine($"INSERT INTO [$(DBName)].[$(DBSchema)].[SystemUsers] (Id,Email,PrincipalName) VALUES ('$(UserGuid)', '$(UserEmail)', '$(UserPrincipalName)');");
            sb.AppendLine($"INSERT INTO [$(DBName)].[$(DBSchema)].[SecurityRoles] (Name, Description, Id,ModifiedOn,CreatedOn,CreatedById,ModifiedById,OwnerId) VALUES('System Administrator', 'Access to all permissions', @adminSRId, CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'{adminSGId}','{adminSGId}','{adminSGId}')");
            sb.AppendLine($"INSERT INTO [$(DBName)].[$(DBSchema)].[SecurityRoleAssignments] (IdentityId, SecurityRoleId, Id,ModifiedOn,CreatedOn,CreatedById,ModifiedById,OwnerId) VALUES('{adminSGId}', @adminSRId, '{Guid.NewGuid()}',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'{adminSGId}','{adminSGId}','{adminSGId}')");
            sb.AppendLine($"INSERT INTO [$(DBName)].[$(DBSchema)].[SecurityGroupMembers] (IdentityId, SecurityGroupId, Id,ModifiedOn,CreatedOn,CreatedById,ModifiedById,OwnerId) VALUES('$(UserGuid)', '{adminSGId}', '{Guid.NewGuid()}',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'{adminSGId}','{adminSGId}','{adminSGId}')");
            sb.AppendLine("END;");
            foreach (var entitiy in model.SelectToken("$.entities").OfType<JProperty>())
            {
                WritePermissionStatement(sb, entitiy, "ReadGlobal", "Global Read", adminSGId, true);
                WritePermissionStatement(sb, entitiy, "Read", "Read", adminSGId);
                WritePermissionStatement(sb, entitiy, "UpdateGlobal", "Global Update", adminSGId, true);
                WritePermissionStatement(sb, entitiy, "Update", "Update", adminSGId);
                WritePermissionStatement(sb, entitiy, "CreateGlobal", "Global Create", adminSGId, true);
                WritePermissionStatement(sb, entitiy, "Create", "Create", adminSGId);
                WritePermissionStatement(sb, entitiy, "DeleteGlobal", "Global Delete", adminSGId, true);
                WritePermissionStatement(sb, entitiy, "Delete", "Delete", adminSGId);
                WritePermissionStatement(sb, entitiy, "ShareGlobal", "Global Share", adminSGId, true);
                WritePermissionStatement(sb, entitiy, "Share", "Share", adminSGId);
                WritePermissionStatement(sb, entitiy, "AssignGlobal", "Global Assign", adminSGId, true);
                WritePermissionStatement(sb, entitiy, "Assign", "Assign", adminSGId);
            }
          
            await File.WriteAllTextAsync($"{outputDirectory}/init-systemadmin.sql", sb.ToString());
        }
        private static void WritePermissionStatement(StringBuilder sb, JProperty entitiy, string permission, string permissionName, string adminSGId, bool adminSRId1 = false)
        {
            sb.AppendLine($"SET @permissionId = ISNULL((SELECT s.Id   FROM [$(DBName)].[$(DBSchema)].[Permissions] s WHERE s.Name = '{entitiy.Value.SelectToken("$.collectionSchemaName")}{permission}'),'{Guid.NewGuid()}')");
            sb.AppendLine($"IF NOT EXISTS(SELECT * FROM [$(DBName)].[$(DBSchema)].[Permissions] WHERE [Name] = '{entitiy.Value.SelectToken("$.collectionSchemaName")}{permission}')");
            sb.AppendLine("BEGIN");
            sb.AppendLine($"INSERT INTO [$(DBName)].[$(DBSchema)].[Permissions] (Name, Description, Id, ModifiedOn,CreatedOn,CreatedById,ModifiedById,OwnerId) VALUES('{entitiy.Value.SelectToken("$.collectionSchemaName")}{permission}', '{permissionName} access to {entitiy.Value.SelectToken("$.pluralName")}', @permissionId, CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'{adminSGId}','{adminSGId}','{adminSGId}')");
            sb.AppendLine("END");
            if (adminSRId1)
            {
                sb.AppendLine($"IF NOT EXISTS(SELECT * FROM [$(DBName)].[$(DBSchema)].[SecurityRolePermissions] WHERE [Name] = 'System Administrator - {entitiy.Value.SelectToken("$.collectionSchemaName")} - {permission}')");
                sb.AppendLine("BEGIN");
                sb.AppendLine($"INSERT INTO [$(DBName)].[$(DBSchema)].[SecurityRolePermissions] (Name, PermissionId, SecurityRoleId, Id,ModifiedOn,CreatedOn,CreatedById,ModifiedById,OwnerId) VALUES('System Administrator - {entitiy.Value.SelectToken("$.collectionSchemaName")} - {permission}', @permissionId, @adminSRId, '{Guid.NewGuid()}', CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,'{adminSGId}','{adminSGId}','{adminSGId}')");
                sb.AppendLine("END");
            }
        }
    }
}
