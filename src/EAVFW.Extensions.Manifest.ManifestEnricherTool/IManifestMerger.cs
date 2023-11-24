using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool
{
    public interface IManifestMerger
    {
        /// <summary>
        /// Component Manifest files are loaded based on the <see cref="path"/>.
        /// </summary>
        /// <param name="path">Path for the root manifest file, <code>manifest.json</code></param>
        public Task<JToken> MergeManifests(FileInfo path);
    }
}
