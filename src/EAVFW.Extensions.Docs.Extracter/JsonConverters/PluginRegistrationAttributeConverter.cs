using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EAVFramework.Plugins;

namespace EAVFW.Extensions.Docs.Extracter
{
    public class PluginRegistrationAttributeConverter : JsonConverter<PluginRegistrationAttribute>
    {
        public override PluginRegistrationAttribute? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(
            Utf8JsonWriter writer,
            PluginRegistrationAttribute value,
            JsonSerializerOptions options)
        {
            var _object = new JsonObject
            {
                { nameof(value.Order), value.Order },
                { nameof(value.Operation), value.Operation.ToString() },
                { nameof(value.Execution), value.Execution.ToString() },
                { nameof(value.Mode), value.Mode.ToString() }
            };
            _object.WriteTo(writer);
        }
    }
}
