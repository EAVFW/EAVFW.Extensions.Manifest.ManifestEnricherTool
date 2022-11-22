


// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

public class InstallCommand : Command
{
    private readonly IHttpClientFactory httpClientFactory;
    public Option<string> Version = new Option<string>("--version", "The version to install");
    public Argument<string> PackageName = new Argument<string>("PackageName", "The package to install, a valid nuget package");

    public Option<string> ShortName = new Option<string>("ShortName", "If provided, the installed extension is not merged in but added as a seperate module");


    public InstallCommand(IHttpClientFactory httpClientFactory) : base("install", "installs a manifest extesion")
    {
        Version.AddAlias("-v");
        Version.IsRequired = false;

        Add(Version);

        ShortName.IsRequired = false;
        Add(ShortName);

        Add(PackageName);

        Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
        this.httpClientFactory=httpClientFactory;
    }

    private async Task Run(ParseResult parseResult, IConsole console)
    {
        var packageName= parseResult.GetValueForArgument(PackageName);
        var packageVersion = parseResult.GetValueForOption(Version);
        console.WriteLine("Installing :" + parseResult.GetValueForArgument(PackageName));
        var http = httpClientFactory.CreateClient();

        var result=await http.GetStringAsync($"https://api.nuget.org/v3/index.json");
        console.WriteLine(result);

        var services = JToken.Parse(result).SelectTokens("$.resources[?(@['@type']=='SearchQueryService')]['@id']");
        console.WriteLine(string.Join(",",services));

        var searches = JToken.Parse(await http.GetStringAsync($"{services.FirstOrDefault()}?q={packageName}&skip={0}&take={10}&prerelease={false}&semVerLevel=2.0.0"));

        console.WriteLine(searches.ToString(Newtonsoft.Json.Formatting.Indented));
        var package = searches.SelectToken("$.data[0]");
        var version = package?.SelectToken("$.version")?.ToString();
        var id = package?.SelectToken("$.id")?.ToString();



        var downlaodservice = JToken.Parse(result).SelectTokens("$.resources[?(@['@type']=='PackageBaseAddress/3.0.0')]['@id']");

        var data = await http.GetByteArrayAsync($"{downlaodservice.FirstOrDefault()}{id}/{version}/{id}.{version}.nupkg".ToLowerInvariant());
        console.WriteLine(data?.Length.ToString()??"");

        using var content = new ZipArchive(new MemoryStream(data));

        var file = content.GetEntry("eavfw/manifest/manifest.extensions.json");

        using var extensions = new StreamReader(file.Open());
        var manifest = JToken.ReadFrom(new JsonTextReader(extensions)) as JObject;

        console.WriteLine(manifest.ToString(Formatting.Indented));

        var manifestFilePath = Directory.GetFiles(Directory.GetCurrentDirectory(), "manifest.json", SearchOption.AllDirectories)
            .Where(c=>Directory.GetFiles(Path.GetDirectoryName(c),"*.csproj").Any())
            .SingleOrDefault();

        var shortName = parseResult.GetValueForOption(ShortName);
        if (!string.IsNullOrEmpty(shortName))
        {
            var fileName = Path.ChangeExtension(manifestFilePath, $".{shortName}.json");

            if (File.Exists(fileName))
            {
                var original = JToken.Parse(File.ReadAllText(fileName)) as JObject;

                manifest.Merge(original, new JsonMergeSettings
                {
                    // union array values together to avoid duplicates
                    MergeArrayHandling = MergeArrayHandling.Union,
                    PropertyNameComparison = System.StringComparison.OrdinalIgnoreCase,
                    MergeNullValueHandling = MergeNullValueHandling.Ignore
                });

                File.WriteAllText(fileName, manifest.ToString(Formatting.Indented));
            }
            else
            {
                File.WriteAllText(fileName, manifest.ToString(Formatting.Indented));
            }
        }
        else if (!string.IsNullOrEmpty(manifestFilePath))
        {
            File.Copy(manifestFilePath, Path.ChangeExtension(manifestFilePath,$".{Directory.GetFiles(Path.GetDirectoryName(manifestFilePath), "*.bac.json").Count()}.bac.json"));

            var original = JToken.Parse(File.ReadAllText(manifestFilePath)) as JObject;

            manifest.Merge(original, new JsonMergeSettings
            {
                // union array values together to avoid duplicates
                MergeArrayHandling = MergeArrayHandling.Union,
                PropertyNameComparison = System.StringComparison.OrdinalIgnoreCase,
                MergeNullValueHandling = MergeNullValueHandling.Ignore
            });

            File.WriteAllText(manifestFilePath, manifest.ToString( Formatting.Indented));

        }


        var csprojct = Directory.GetFiles(Path.GetDirectoryName(manifestFilePath), "*.csproj");
        console.WriteLine($"Found {csprojct.Length} projects for '{manifestFilePath}'");
        if (csprojct.Length==1)
        {
            var proj = XDocument.Load(csprojct[0]);

            var clean_itemgroup_elements = proj.Root.Elements("ItemGroup").Where(ig => ig.Elements().All(n=>n.Name == "PackageReference") && !ig.Attributes("Condition").Any() ).ToArray();

            console.WriteLine($"Found {clean_itemgroup_elements.Length} itemgroup elements with all packageReferences");

            var with_packages = clean_itemgroup_elements.SelectMany(ig => ig.Elements("PackageReference"))
                .Where(k => string.Equals(k.Attribute("Include").Value, id, System.StringComparison.OrdinalIgnoreCase))
                .ToArray();

            console.WriteLine($"Found {with_packages.Length}  package refrences for '{id}'");

            if (!with_packages.Any())
            {
                if (clean_itemgroup_elements.Any())
                {
                    clean_itemgroup_elements.FirstOrDefault().Add(new XElement("PackageReference", new XAttribute("Include",id), new XAttribute("Version",version)));

                   
                }
                else
                {
                    proj.Root.Add(new XElement("ItemGroup", new XElement("PackageReference", new XAttribute("Include", id), new XAttribute("Version", version))));
                }

                console.WriteLine($"Saving '{csprojct[0]}'");
                proj.Save(csprojct[0]);
            }

          


        }


    }
}
