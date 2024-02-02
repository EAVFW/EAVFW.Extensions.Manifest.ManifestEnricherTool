using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EAVFW.Extensions.Docs.Extractor;
using EAVFW.Extensions.Manifest.SDK;

namespace EAVFW.Extensions.Docs.Generator
{
    public interface IDocumentationGenerator
    {
        public void AddPluginSource(IEnumerable<PluginDocumentation> pluginDocumentations);
        public void AddWizardSource(Dictionary<string, EntityDefinition> entitiesWithWizards);
        public void AddGeneratedManifest(ManifestDefinition generatedManifest);
        public Task Write(FileInfo outputLocation, string component);
    }
}
