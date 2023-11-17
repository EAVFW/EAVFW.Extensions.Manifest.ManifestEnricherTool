using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EAVFW.Extensions.Docs.Extracter
{
    public class TypeConverter : JsonConverter<Type>
    {
        public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
        {
            JsonValue.Create(value.Name).WriteTo(writer, options);
        }
    }
}
