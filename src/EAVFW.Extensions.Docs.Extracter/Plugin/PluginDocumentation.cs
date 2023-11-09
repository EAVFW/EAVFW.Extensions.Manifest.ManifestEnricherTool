using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using EAVFramework.Plugins;

namespace EAVFW.Extensions.Docs.Extracter
{
    public class PluginDocumentation
    {
        public IEnumerable<PluginRegistrationAttribute> PluginRegistrations { get; set; } =
            Array.Empty<PluginRegistrationAttribute>();

        public string? Name { get; set; }

        [JsonConverter(typeof(TypeConverter))] public Type? Context { get; set; }

        [JsonConverter(typeof(TypeConverter))] public Type? Entity { get; set; }
        public string Summary { get; set; } = "";
    }
}
