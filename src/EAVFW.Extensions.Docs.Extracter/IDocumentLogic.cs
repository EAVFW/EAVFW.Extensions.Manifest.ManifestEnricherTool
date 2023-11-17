using System.Collections.Generic;
using System.IO;
using EAVFW.Extensions.Manifest.SDK;

namespace EAVFW.Extensions.Docs.Extracter
{
    public interface IDocumentLogic
    {
        /// <summary>
        /// Extract plugin metadata and summary from given Assembly using the DLL and .xml documentation files
        /// created during a build.
        ///
        /// Remember to enable GenerateDocumentationFile for the project.
        /// <param name="pluginInfo"></param>
        /// </summary>
        IEnumerable<PluginDocumentation> ExtractPluginDocumentation(PluginInfo pluginInfo);

        /// <summary>
        /// Extract Wizards from the given Manifest and generate documentation based on manifest metadata and workflow
        /// CLR types and Actions
        /// </summary>
        /// <param name="manifestFile"></param>
        /// <param name="pluginInfo"></param>
        /// <returns></returns>
        Dictionary<string, EntityDefinition> ExtractWizardDocumentation(FileInfo manifestFile, PluginInfo pluginInfo);
    }
}
