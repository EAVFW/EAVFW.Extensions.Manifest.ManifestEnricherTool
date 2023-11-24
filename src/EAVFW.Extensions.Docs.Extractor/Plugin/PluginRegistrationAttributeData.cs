using EAVFramework.Plugins;

namespace EAVFW.Extensions.Docs.Extractor
{
    public class PluginRegistrationAttributeData
    {
        public EntityPluginExecution Execution { get; set; }
        public EntityPluginOperation Operation { get; set; }
        public EntityPluginMode Mode { get; set; }
        public int Order { get; set; }
    }
}
