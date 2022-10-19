using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
{
    
    public class ManifestNewMigrationCommand : Command
    {
        public Argument<string> ProjectPath = new Argument<string>("--project-path", "The project path to EAV Model Project");
        public Option<string> SubModelName = new Option<string>("--sub-module-name", "Generate a new sub model with this name");

        private readonly ILogger<ManifestNewMigrationCommand> logger;

        public ManifestNewMigrationCommand(ILogger<ManifestNewMigrationCommand> logger) : base("new-migration", "Create new migration")
        {
            this.logger = logger;



            ProjectPath.SetDefaultValue(".");
            Add(ProjectPath);

            SubModelName.AddAlias("-s");
           
            Add(SubModelName);

            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
        }
        private async Task Run(ParseResult parseResult, IConsole console)
        {
           
            
            using var repo = new Repository(ProjectPath.GetValue(parseResult));

            if(repo.Diff.Compare<TreeChanges>(new[] { "*manifest*.json" }).Any())
            {

                logger.LogWarning("Please commit or stash your current changes.");
                return;
            }

            //TODO run the generate command first.

            var files = Directory.GetFiles(ProjectPath.GetValue(parseResult), "*manifest.g.json", SearchOption.AllDirectories)
                .Where(f=>!f.Replace("\\","/").Contains("/bin/"));

            if (!files.Any() || files.Skip(1).Any())
            {
                logger.LogWarning("The generated Manifest was not found: {Files}", String.Join(",",files));
                return;

            }

            var file = files.Single();
            var manifest_gen = JToken.Parse(File.ReadAllText(file));
            var version_gen = manifest_gen.SelectToken("$.version");
            File.Copy(file, $"{Path.GetDirectoryName(file)}/../manifests/manifest.{version_gen}.g.json");

            var semversion = Semver.SemVersion.Parse(version_gen.ToString(), Semver.SemVersionStyles.Strict);

            var manifst = JToken.Parse(File.ReadAllText($"{Path.GetDirectoryName(file)}/../manifest.json"));
            var version = manifst.SelectToken("$.version");
            version.Replace(semversion.WithPatch(semversion.Patch + 1).ToString());
            File.WriteAllText($"{Path.GetDirectoryName(file)}/../manifest.json", manifst.ToString(Formatting.Indented));

            var name = SubModelName.GetValue(parseResult);
            if (!string.IsNullOrEmpty(name))
            {
                var model = $"{Path.GetDirectoryName(file)}/../manifest.{name.ToLower()}.json";
                File.WriteAllText(model,
                    JToken.FromObject(new { variables = new { sitemaps=new Dictionary<string,object>{ [name] = new { app="", area=name, group=""} } },entities = new { }  }).ToString(Formatting.Indented));

                var csproj = Directory.GetFiles(Path.GetDirectoryName(model),"*.csproj").Single();

                var project = XElement.Load(csproj);

                var extensions = project.Element("ProjectExtensions").Element("VisualStudio").Element("UserProperties");

                extensions.Add(new XAttribute( $"manifest.{name.ToLower()}.json__JsonSchema".Replace(".","_1") ,"manifest.schema.json"));

                var BuildIfChanged = project.Elements("Target").FirstOrDefault(n => n.Attribute("Name")?.Value == "BuildIfChanged");
                var attr = BuildIfChanged.Attribute("Inputs");

                attr.Value = attr.Value + $";$(MSBuildProjectDirectory)/manifest.{name.ToLower()}.json";

                project.Save(csproj);
            }

            logger.LogInformation("Done");
        }
    }
}
