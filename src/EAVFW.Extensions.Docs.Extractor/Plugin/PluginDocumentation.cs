using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EAVFW.Extensions.Docs.Extractor
{
    public class PluginDocumentation
    {
        public IEnumerable<PluginRegistrationAttributeData> PluginRegistrations { get; set; } =
            Array.Empty<PluginRegistrationAttributeData>();

        public string? Name { get; set; }

        [JsonPropertyName("context")] public TypeInformation Context { get; set; }
        
        [JsonPropertyName("entity")] public TypeInformation Entity { get; set; }
        
        public string Summary { get; set; } = "";
    }
}
