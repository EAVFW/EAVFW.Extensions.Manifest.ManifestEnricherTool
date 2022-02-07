


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

    public InstallCommand(IHttpClientFactory httpClientFactory) : base("install", "installs a manifest extesion")
    {
        Version.AddAlias("-v");
        Version.IsRequired = false;

        Add(Version);


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

var result=await        http.GetStringAsync($"https://api.nuget.org/v3/index.json");
        console.WriteLine(result);

        var services = JToken.Parse(result).SelectTokens("$.resources[?(@['@type']=='SearchQueryService')]['@id']");
        console.WriteLine(string.Join(",",services));

        var searches = JToken.Parse(await http.GetStringAsync($"{services.FirstOrDefault()}?q={packageName}&skip={0}&take={10}&prerelease={false}{(string.IsNullOrEmpty(packageVersion) ? "" : $"&semVerLevel={packageVersion}")}"));

        console.WriteLine(searches.ToString(Newtonsoft.Json.Formatting.Indented));
        var package = searches.SelectToken("$.data[0]");
        var version = package?.SelectToken("$.version")?.ToString();
        var id = package?.SelectToken("$.id")?.ToString();

        var downlaodservice = JToken.Parse(result).SelectTokens("$.resources[?(@['@type']=='PackageBaseAddress/3.0.0')]['@id']"); 
        var data = await http.GetByteArrayAsync($"{downlaodservice.FirstOrDefault()}{id}/{version}/{id}.{version}.nupkg");
        console.WriteLine(data?.Length.ToString()??"");

        using var content = new ZipArchive(new MemoryStream(data));

        var file = content.GetEntry("eavfw/manifest/manifest.extensions.json");

        using var extensions = new StreamReader(file.Open());
        var manifest = JToken.ReadFrom(new JsonTextReader(extensions));

        console.WriteLine(manifest.ToString(Formatting.Indented));

        if (File.Exists("manifest.json"))
        {
            File.Copy("manifest.json", "manifest.bac.json");

            var original = JToken.Parse(File.ReadAllText("manifest.json")) as JObject;

            original.Merge(manifest, new JsonMergeSettings
            {
                // union array values together to avoid duplicates
                MergeArrayHandling = MergeArrayHandling.Union,
                PropertyNameComparison = System.StringComparison.OrdinalIgnoreCase
            });

            File.WriteAllText("manifest.json", original.ToString( Formatting.Indented));

        }

        var csprojct = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
        if (csprojct.Length==1)
        {
            var proj = XDocument.Load(csprojct[0]);

            var itgs = proj.Root.Elements("ItemGroup").Where(ig => ig.Elements("PackageReference").Any()).ToArray();
            var packages = itgs.SelectMany(ig => ig.Elements("PackageReference")).ToDictionary(k=>k.Attribute("Include"),v=>v.Attribute("Version"));
            if (!packages.Any())
            {
                if (itgs.Any())
                {
                    itgs.FirstOrDefault().Add(new XElement("PackageReference", new XAttribute("Include",id), new XAttribute("Version",version)));

                    proj.Save(csprojct[0]);
                }
            }

          


        }


    }
}
