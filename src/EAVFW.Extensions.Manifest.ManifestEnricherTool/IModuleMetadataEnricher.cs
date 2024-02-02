using Newtonsoft.Json.Linq;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool
{
    public interface IModuleMetadataEnricher
    {
        /// <summary>
        /// Add metadata to each module before merging and enriching manifests.
        /// </summary>
        /// <param name="manifest">The module manifest</param>
        /// <param name="source">The module manifest json file name</param>
        public void AddSource(JToken manifest, string source);
    }
}
