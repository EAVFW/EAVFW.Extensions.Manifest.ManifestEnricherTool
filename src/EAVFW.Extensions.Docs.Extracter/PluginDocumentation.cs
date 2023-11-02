using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using EAVFramework.Plugins;

namespace EAVFW.Extensions.Docs.Extracter
{
    public class PluginDocumentation
    {
        [JsonIgnore]
        public IEnumerable<PluginRegistrationAttribute> PluginRegistrations { get; set; } =
            Array.Empty<PluginRegistrationAttribute>();

        public string? Name { get; set; }
        
        [JsonIgnore]
        public Type? Context { get; set; }
        [JsonIgnore]
        public Type? Entity { get; set; }
        public string Summary { get; set; } = "";


        public override string ToString()
        {
            var summary = Summary?.Split("\n").Select(x => x.Trim()).ToArray() ?? Array.Empty<string>();
            return $"Plugin: {Name} on {Entity.Name}\n* " 
                   + string.Join("\n* ", PluginRegistrations.Select(x => $"{x.Operation} on {x.Execution} as {x.Order}"))
                   + '\n'
                   + string.Join('\n', summary) 
                   + "\n\n";
        }
    }
}
