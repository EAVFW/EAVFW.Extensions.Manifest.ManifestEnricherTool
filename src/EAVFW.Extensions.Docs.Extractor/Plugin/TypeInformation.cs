using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace EAVFW.Extensions.Docs.Extractor
{
    public class TypeInformation
    {
        [JsonPropertyName("AssemblyQualifiedName")]
        public string AssemblyQualifiedName { get; set; }

        [JsonIgnore]
        public string Name => AssemblyQualifiedName.Split(',').First().Split('.').Last().Trim();

        public TypeInformation()
        {
        }

        public TypeInformation(Type type)
        {
            AssemblyQualifiedName = type.AssemblyQualifiedName;
        }
    }
}
