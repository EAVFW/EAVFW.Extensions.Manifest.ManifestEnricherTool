using System.Collections.Generic;

namespace EAVFW.Extensions.Docs.Extracter
{
    public interface IDocumentLogic
    {
        /// <summary>
        /// Extract plugin metadata and summary from given Assembly using the DLL and .xml documentation files
        /// created during a build.
        ///
        /// Remember to enable GenerateDocumentationFile for the project.
        /// </summary>
        IEnumerable<PluginDocumentation> ExtractPluginDocumentation(PluginInfo pluginInfo);
    }
}
