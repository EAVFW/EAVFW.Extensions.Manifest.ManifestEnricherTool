using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
                    PropertyNameComparison = System.StringComparison.OrdinalIgnoreCase,
                    MergeNullValueHandling = MergeNullValueHandling.Ignore
                });
            }

            return jsonRaw;
        }
    }
}
