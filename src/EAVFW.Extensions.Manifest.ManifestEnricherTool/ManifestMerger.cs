using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.FileSystemGlobbing;
using Newtonsoft.Json.Linq;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool
{
    public class ManifestMerger : IManifestMerger
    {
        private readonly IModuleMetadataEnricher _moduleMetadataEnricher;

        public ManifestMerger(IModuleMetadataEnricher moduleMetadataEnricher)
        {
            _moduleMetadataEnricher =
                moduleMetadataEnricher ?? throw new ArgumentNullException(nameof(moduleMetadataEnricher));
        }

        /// <inheritdoc/>
        public async Task<JToken> MergeManifests(FileInfo path)
        {
            await using var fs = File.OpenRead(path.FullName);

            var jtokenRaw = await JToken.ReadFromAsync(new Newtonsoft.Json.JsonTextReader(new StreamReader(fs)));

            _moduleMetadataEnricher.AddSource(jtokenRaw, path.Name);
            var jsonRaw = jtokenRaw as JObject;

            var others = Directory.GetFiles(path.DirectoryName!, "manifest.*.json")
                .Where(c => !string.Equals("manifest.schema.json", System.IO.Path.GetFileName(c),
                    StringComparison.OrdinalIgnoreCase));

            foreach (var other in others)
            {
                var fileInfo = new FileInfo(other);

                var component = JToken.Parse(await File.ReadAllTextAsync(fileInfo.FullName));

                _moduleMetadataEnricher.AddSource(component, fileInfo.Name);
                jsonRaw.Merge(component, new JsonMergeSettings
                {
                    // union array values together to avoid duplicates
                    MergeArrayHandling = MergeArrayHandling.Union,
                    PropertyNameComparison = StringComparison.OrdinalIgnoreCase,
                    MergeNullValueHandling = MergeNullValueHandling.Ignore
                });
            }

            var matcher = new Matcher();
            matcher.AddInclude("**/entities/*");
            matcher.AddInclude("**/attributes/*");
            matcher.AddInclude("**/wizards/*");

            CleanModuleLocationSource(jtokenRaw, matcher, "root");

            return jsonRaw;
        }

        private static void CleanModuleLocationSource(JToken jToken, Matcher matcher, string path)
        {
            switch (jToken)
            {
                case JObject jObject:
                {
                    if (!matcher.Match(path).HasMatches)
                    {
                        jObject.Remove("moduleSource");
                        jObject.Remove("moduleLocation");
                    }
                    
                    foreach (var (key, value) in jObject)
                        CleanModuleLocationSource(value, matcher, $"{path}/{key}");
                    break;
                }
                case JArray jArray:
                {
                    foreach (var child in jArray.Children())
                        CleanModuleLocationSource(child, matcher, path);
                    break;
                }
            }
        }
    }
}
