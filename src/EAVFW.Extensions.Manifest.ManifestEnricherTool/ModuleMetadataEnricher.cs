using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool
{
    public class ModuleMetadataEnricher : IModuleMetadataEnricher
    {
        /// <inheritdoc/>
        public void AddSource(JToken jToken, string source)
        {
            var entities = jToken.SelectToken("$.entities")?.Children();

            if (entities == null) return;

            foreach (var entity in entities)
            {
                (entity.First as JObject)?.Add("moduleSource", source);
                (entity.First as JObject)?.Add("moduleLocation", "manifest");

                var attributes = entity.First?.SelectToken("$.attributes")?.Children();

                if (attributes == null) continue;

                foreach (var attribute in attributes)
                {
                    (attribute.First as JObject)?.Add("moduleSource", source);
                    (attribute.First as JObject)?.Add("moduleLocation", "entity");
                }
            }

            var variables = jToken.SelectToken("$.variables");
            if (variables == null)
                return;
            
            foreach (var child in variables.Children())
            {
                WalkJToken(child, source);
            }
        }

        private void WalkJToken(JToken jToken, string source)
        {
            switch (jToken)
            {
                case JProperty jProperty:
                    // jp.Add("moduleSource", source);
                    // jp.Add("moduleLocation", "variables");
                    foreach (var child in jProperty.Children())
                    {
                        WalkJToken(child, source);
                    }
                    
                    break;
                case JObject jObject:
                    jObject.Add("moduleSource", source);
                    jObject.Add("moduleLocation", "variables");
                    foreach (var child in jObject.Children())
                    {
                        WalkJToken(child, source);
                    }

                    break;
                case JArray jArray:
                    foreach (var child in jArray.Children())
                    {
                        WalkJToken(child, source);
                    }

                    break;
            }
        }
    }
}
