using DotNETDevOps.JsonFunctions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool
{

    public partial class ManifestCommand
    {

        public string Path { get; set; } = Directory.GetCurrentDirectory();

        private static string? ToSchemaName(string? displayName)
        {
            return displayName?.Replace(" ", "").Replace(":", "_").Replace("/", "or").Replace("-", "").Replace("(", "").Replace(")", ""); ;
        }

        private JObject Merge(JObject jToken, object obj)
        {
          
            jToken = (JObject)jToken.DeepClone();


            var jobj = JToken.FromObject(obj) as JObject;

            foreach (var p in jobj.Properties())
            {
                if (!(p.Value.Type == JTokenType.Null || p.Value.Type == JTokenType.Undefined))
                    jToken[p.Name] = p.Value;
            }

            if (!jToken.ContainsKey("schemaName"))
                jToken["schemaName"] = ToSchemaName(jToken.SelectToken("$.displayName")?.ToString());
            if (!jToken.ContainsKey("logicalName"))
                jToken["logicalName"] = jToken.SelectToken("$.schemaName")?.ToString().ToLower();

            return jToken as JObject;
        }
        private JObject CreateAttribute(JObject attr, string displayName, object type, string schemaName = null, object additionalProps = null)
        {
            if (additionalProps != null)
                return Merge(Merge(attr, new { displayName, type, schemaName }), additionalProps);
            return Merge(attr, new { displayName, type, schemaName });
        }
        private object[] CreateOptions(params string[] args)
        {
            return args.Select((o, i) => new { label = o, value = i + 1 }).ToArray();
        }

        public async Task<JsonDocument> LoadJsonDocumentAsync(FileStream fs, string customizationprefix, ILogger logger)
        {


            var jsonraw = Newtonsoft.Json.Linq.JToken.ReadFrom(new Newtonsoft.Json.JsonTextReader(new StreamReader(fs)));
            var insertMerges = jsonraw.SelectToken("$.variables.options.insertMergeLayoutVariable")?.ToObject<string>();

            foreach (var entitieP in (jsonraw.SelectToken("$.entities") as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                var entity = (JObject)entitieP.Value;

                if (!entity.ContainsKey("displayName"))
                    entity["displayName"] = entitieP.Name;
                if (!entity.ContainsKey("schemaName"))
                    entity["schemaName"] = entity.SelectToken("$.displayName")?.ToString().Replace(" ", "");
                if (!entity.ContainsKey("logicalName"))
                    entity["logicalName"] = entity.SelectToken("$.schemaName")?.ToString().ToLower();

                if (!entity.ContainsKey("collectionSchemaName"))
                    entity["collectionSchemaName"] = ToSchemaName(entity["pluralName"]?.ToString());

                JObject SetDefault(JToken obj, JObject localeEnglish)
                {
                    var value = new JObject(new JProperty("1033", localeEnglish));
                    obj["locale"] = value;
                    return value;
                }
                var entityLocaleEnglish = new JObject(new JProperty("displayName", entity["displayName"]), new JProperty("pluralName", entity["pluralName"]));
                var entityLocale = entity.SelectToken("$.locale") as JObject ?? SetDefault(entity, entityLocaleEnglish);
                if (!entityLocale.ContainsKey("1033"))
                    entityLocale["1033"] = entityLocaleEnglish;


                var attributes = entitieP.Value.SelectToken("$.attributes") as JObject;

                if (attributes == null)
                {
                    entitieP.Value["attributes"] = attributes = new JObject();
                }

                if (attributes != null)
                {
                    if (!attributes.Properties().Any(p => p.Value.SelectToken("$.isPrimaryKey")?.ToObject<bool>() ?? false))
                    {
                        attributes["Id"] = JToken.FromObject(new { isPrimaryKey = true, type = "guid" });
                    }


                    //Replace string attributes
                    foreach (var attr in attributes.Properties().ToArray())
                    {
                        if (attr.Name == "[merge()]")
                        {
                            await RunReplacements(jsonraw, customizationprefix,logger, attr);
                        }
                        else if (attr.Value.Type == JTokenType.String)
                        {
                            await RunReplacements(jsonraw, customizationprefix,logger, attr.Value);
                        }
                    }

                    var queue = new Queue<JObject?>(attributes.Properties().Select(c => c.Value as JObject));

                    foreach (var attribute in attributes.Properties())
                    {
                        if (!string.IsNullOrEmpty(insertMerges))
                        {
                            var value = attribute.Value as JObject;
                            if (!value?.ContainsKey("[merge()]")??false)
                                value.Add(new JProperty("[merge()]", $"[variables('{insertMerges}')]"));
                            queue.Enqueue(value);
                        }
                    }


                    while (queue.Count > 0)
                    {
                        var attr = queue.Dequeue();



                        if (!attr.ContainsKey("displayName"))
                            attr["displayName"] = (attr.Parent as JProperty)?.Name;

                        if (attr["type"]?.ToString() == "address")
                        {

                            var displayName = attr.SelectToken("$.displayName")?.ToString();

                            attr["__unroll__path"] = attr.Path;

                            var unrolls = new[] {
                            Merge(attr,new { displayName=$"{displayName}: Address Type", type=new { type ="picklist",
                                isGlobal=false,
                                name=$"{displayName}: Address Type",
                                options=CreateOptions("Bill To","Ship To","Primary","Other")
                            } }),
                            Merge(attr,new { displayName=$"{displayName}: City", type="string"}),
                            Merge(attr,new { displayName=$"{displayName}: Country", type="string", schemaName=ToSchemaName( $"{displayName}: Country")}),
                            Merge(attr,new { displayName=$"{displayName}: County", type="string"}),
                            Merge(attr,new { displayName=$"{displayName}: Fax", type="string"}),
                            Merge(attr,new { displayName=$"{displayName}: Freight Terms", schemaName=ToSchemaName( $"{displayName}: Freight Terms Code"), type=new { type="picklist",
                                isGlobal=false,
                                name=$"{displayName}: Freight Terms",
                                options=CreateOptions("FOB","No Charge")
                            } }),
                           // Merge(attr,new { displayName=$"{displayName}: Id",schemaName=ToSchemaName( $"{displayName}: AddressId"),type ="guid"}),
                            CreateAttribute(attr,$"{displayName}: Latitude","float"),
                            CreateAttribute(attr,$"{displayName}: Longitude","float"),
                            CreateAttribute(attr,$"{displayName}: Name","string",null, new { isPrimaryField = !attributes.Properties().Any(p=>p.Value.SelectToken("$.isPrimaryField") != null) }),
                            CreateAttribute(attr,$"{displayName}: Phone","phone", ToSchemaName( $"{displayName}: Telephone 1")),
                            CreateAttribute(attr,$"{displayName}: Telephone 2","phone", ToSchemaName( $"{displayName}: Telephone 2")),
                            CreateAttribute(attr,$"{displayName}: Telephone 3","phone", ToSchemaName( $"{displayName}: Telephone 3")),
                            CreateAttribute(attr,$"{displayName}: Post Office Box","string"),
                            CreateAttribute(attr,$"{displayName}: Primary Contact Name","string"),
                            CreateAttribute(attr,$"{displayName}: Shipping Method",new { type="picklist",
                                isGlobal=false,
                                name=$"{displayName}: Shipping Method",
                                options=CreateOptions("Airborne","DHL","FedEx","UPS","Postal Mail","Full Load","Will Call"),
                            }, ToSchemaName( $"{displayName}: Shipping Method Code")),
                            CreateAttribute(attr,$"{displayName}: State/Province","string"),
                            CreateAttribute(attr,$"{displayName}: Street 1","string",ToSchemaName( $"{displayName}: line1")),
                            CreateAttribute(attr,$"{displayName}: Street 2","string",ToSchemaName( $"{displayName}: line2")),
                            CreateAttribute(attr,$"{displayName}: Street 3","string",ToSchemaName( $"{displayName}: line3")),
                            CreateAttribute(attr,$"{displayName}: UPS Zone","string"),
                            CreateAttribute(attr,$"{displayName}: UTC Offset","timezone"),
                            CreateAttribute(attr,$"{displayName}: ZIP/Postal Code","string",ToSchemaName( $"{displayName}: Postal Code")),
                            CreateAttribute(attr,$"{displayName}: State/Province","string"),

                        };

                            attr["schemaName"] = displayName.Replace(" ", "").Replace(":", "_") + "_Composite";
                            attr["type"] = "MultilineText";


                            foreach (var unroll in unrolls)
                            {
                                queue.Enqueue(unroll);
                            }

                            //if(!attributes.Properties().Any(p=>p.Value.SelectToken("$.isPrimaryField") != null))
                            //{
                            //    attr["type"] = JObject.FromObject(new { type = "string", maxLength = 1024 });
                            //    attr["isPrimaryField"] = true;
                            //}

                        }


                        if (!attr.ContainsKey("schemaName"))
                        {

                            attr["schemaName"] = ToSchemaName(attr.SelectToken("$.displayName").ToString());

                            await RunReplacements(jsonraw, customizationprefix, logger,attr);

                            switch (attr.SelectToken("$.type.type")?.ToString()?.ToLower())
                            {
                                case "lookup":
                                case "customer":
                                    if (!attr["schemaName"].ToString().EndsWith("Id"))
                                        attr["schemaName"] = $"{ToSchemaName(attr.SelectToken("$.displayName").ToString())}Id";



                                    break;

                            }
                        }


                        if (!attr.ContainsKey("logicalName"))
                            attr["logicalName"] = attr.SelectToken("$.schemaName").ToString().ToLower();

                        if (!attr.ContainsKey("type"))
                            attr["type"] = "string";

                        if (attr.Parent == null && !(attributes.ContainsKey(attr["logicalName"].ToString()) || attributes.ContainsKey(attr["schemaName"].ToString()) || attributes.ContainsKey(attr["displayName"].ToString())))
                            attributes[attr["logicalName"].ToString()] = attr;

                        if (attr.SelectToken("$.type").Type == JTokenType.String)
                        {
                            attr["type"] = JToken.FromObject(new { type = attr.SelectToken("$.type") });
                        }



                    }

                    foreach (var attr in attributes.Properties())
                    {
                        var attributeLocaleEnglish = new JObject(new JProperty("displayName", attr.Value["displayName"]));
                        var attributeLocale = attr.Value.SelectToken("$.locale") as JObject ?? SetDefault(attr.Value, attributeLocaleEnglish);
                        if (!attributeLocale.ContainsKey("1033"))
                            attributeLocale["1033"] = attributeLocaleEnglish;
                    }


                }

            }


            await RunReplacements(jsonraw, customizationprefix,logger);


            foreach (var entitieP in (jsonraw.SelectToken("$.entities") as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                var attributes = entitieP.Value.SelectToken("$.attributes") as JObject;

                foreach (var attributeDefinition in attributes.Properties())
                {
                    var attr = attributeDefinition.Value;

                    switch (attr.SelectToken("$.type.type")?.ToString()?.ToLower())
                    {
                        case "lookup":

                            attr["type"]["foreignKey"] = JToken.FromObject(new
                            {
                                principalTable = jsonraw.SelectToken($"$.entities['{ attr["type"]["referenceType"] }'].logicalName").ToString(),
                                principalColumn = jsonraw.SelectToken($"$.entities['{ attr["type"]["referenceType"] }'].attributes").OfType<JProperty>()
                                    .Concat(jsonraw.SelectToken($"$.entities['{ attr["type"]["referenceType"] }'].TPT") == null ? Enumerable.Empty<JProperty>() : jsonraw.SelectToken($"$.entities['{jsonraw.SelectToken($"$.entities['{ attr["type"]["referenceType"] }'].TPT") }'].attributes").OfType<JProperty>())
                                    .GroupBy(k => k.Name).Select(g => g.First())
                                    .Single(a => a.Value.SelectToken("$.isPrimaryKey")?.ToObject<bool>() ?? false).Value.SelectToken("$.logicalName").ToString(),
                                principalNameColumn = jsonraw.SelectToken($"$.entities['{ attr["type"]["referenceType"] }'].attributes").OfType<JProperty>()
                                    .Concat(jsonraw.SelectToken($"$.entities['{ attr["type"]["referenceType"] }'].TPT") == null ? Enumerable.Empty<JProperty>() : jsonraw.SelectToken($"$.entities['{jsonraw.SelectToken($"$.entities['{ attr["type"]["referenceType"] }'].TPT") }'].attributes").OfType<JProperty>())
                                    .GroupBy(k => k.Name).Select(g => g.First())
                                    .Single(a => a.Value.SelectToken("$.isPrimaryField")?.ToObject<bool>() ?? false).Value.SelectToken("$.logicalName").ToString(),
                                name = TrimId(attr.SelectToken("$.logicalName")?.ToString()) // jsonraw.SelectToken($"$.entities['{ attr["type"]["referenceType"] }'].logicalName").ToString().Replace(" ", ""),
                            });

                            break;
                        case "float":
                        case "decimal":
                            if (attr.SelectToken("$.type.sql") == null)
                            {
                                attr["type"]["sql"] = JToken.FromObject(new { precision = 18, scale = 4 });
                            }
                            if (attr.SelectToken("$.type.sql.precision") == null)
                            {
                                attr["type"]["sql"]["precision"] = 18;
                            }
                            if (attr.SelectToken("$.type.sql.scale") == null)
                            {
                                attr["type"]["sql"]["scale"] = 4;
                            }
                            break;

                    }


                }
            }



            var defaultControls = jsonraw.SelectToken("$.controls");
            if (defaultControls != null)
            {
                logger.LogInformation("Replacing default Controls");

                foreach (var defaultControl in defaultControls.OfType<JProperty>())
                {
                    logger.LogInformation("Replacing default Controls : {Type}", defaultControl.Name);

                    foreach (var entity in jsonraw.SelectToken("$.entities")?.OfType<JProperty>() ?? Enumerable.Empty<JProperty>())
                    {
                        foreach (var attribute in entity.Value.SelectToken("$.attributes")?.OfType<JProperty>() ?? Enumerable.Empty<JProperty>())
                        {
                            var attributeType = (attribute.Value.SelectToken("$.type.type") ?? attribute.Value.SelectToken("$.type")).ToString();

                            if (string.Equals(attributeType, defaultControl.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                logger.LogInformation("Replacing default Controls for {entity} {attribute} : {type}", entity.Name, attribute.Name, defaultControl.Name);

                                var formFields = (entity.Value.SelectToken($"$.forms")?.OfType<JProperty>() ?? Enumerable.Empty<JProperty>())
                                    .Select(c => c.Value.SelectToken($"$.columns['{attribute.Name}']")).Where(c => c != null).ToArray();

                                {

                                    foreach (var formField in formFields)
                                    {
                                        var control = formField.SelectToken("$.control");

                                        if (control == null)
                                        {
                                            var replacement = defaultControl.Value.DeepClone(); ;
                                            formField["control"] = replacement;
                                            var q = new Queue<JToken>(new[] { replacement });
                                            while (q.Any())
                                            {
                                                var e = q.Dequeue();
                                                if (e is JObject obj)
                                                {
                                                    foreach (var prop in e.OfType<JProperty>())
                                                    {
                                                        q.Enqueue(prop);
                                                    }
                                                }
                                                else if (e is JProperty prop)
                                                {
                                                    q.Enqueue(prop.Value);
                                                }
                                                else if (e is JArray array)
                                                {
                                                    foreach (var ee in array)
                                                    {
                                                        q.Enqueue(ee);
                                                    }
                                                }
                                                else if (e.Type == JTokenType.String)
                                                {
                                                    var str = e.ToString();
                                                    if (str.StartsWith("[[") && str.EndsWith("]]"))
                                                    {
                                                        e.Replace(str.Substring(1, str.Length - 2));
                                                    }

                                                }
                                            }


                                            logger.LogInformation("Replacing default Controls for {entity} {attribute} {formname}: {type}", entity.Name, attribute.Name, (formField.Parent.Parent.Parent as JProperty)?.Name, defaultControl.Name);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            await RunReplacements(jsonraw, customizationprefix,logger);


            foreach (var (entityDefinition, attributeDefinition2) in jsonraw.SelectToken("$.entities").OfType<JProperty>()
                .SelectMany(e => e.Value.SelectToken("$.attributes").OfType<JProperty>().Select(p => (e, p)))
                .Where(a => a.p.Value.SelectToken("$.type.type")?.ToString().ToLower() == "choices")
                .ToArray())
            {




                var nentity = $"{attributeDefinition2.Value.SelectToken("$.type.name")}";


                jsonraw["entities"][nentity] = JToken.FromObject(
                   new
                   {
                       pluralName = $"{attributeDefinition2.Value.SelectToken("$.type.pluralName")}",
                       displayName = nentity,
                       logicalName = $"{attributeDefinition2.Value.SelectToken("$.type.name")}".Replace(" ", "").ToLower(),
                       schemaName = $"{attributeDefinition2.Value.SelectToken("$.type.name")}".Replace(" ", ""),
                       collectionSchemaName = $"{attributeDefinition2.Value.SelectToken("$.type.pluralName")}".Replace(" ", ""),
                       keys = new Dictionary<string, object>
                       {
                           [$"IX_{entityDefinition.Name}Value"] = new[] { entityDefinition.Name, nentity + " Value" }
                       },
                       attributes = new Dictionary<string, object>
                       {
                           ["Id"] = new
                           {
                               displayName = "Id",
                               logicalName = "id",
                               schemaName = "Id",
                               type = "guid",
                               isPrimaryKey = true,
                           },
                           [entityDefinition.Name] = new
                           {
                               displayName = entityDefinition.Value.SelectToken("$.displayName"),
                               logicalName = entityDefinition.Value.SelectToken("$.logicalName")+"id",
                               schemaName = entityDefinition.Value.SelectToken("$.schemaName") + "Id",
                               type = new
                               {
                                   type = "lookup",
                                   referenceType = entityDefinition.Name,
                               },
                           },
                           [nentity+" Value"] = new
                           {

                               displayName = nentity+" Value",
                               logicalName = $"{attributeDefinition2.Value.SelectToken("$.type.name")}".Replace(" ", "").ToLower(),
                               schemaName = $"{attributeDefinition2.Value.SelectToken("$.type.name")}".Replace(" ", "") +"Value",
                               //   isPrimaryKey = true,
                               type = new
                               {
                                   type = "choice",
                                   name = $"{attributeDefinition2.Value.SelectToken("$.type.name")}".Replace(" ", "") + "Value",
                                   options = attributeDefinition2.Value.SelectToken("$.type.options")
                               }
                           }
                       }
                   });
                //attributeDefinition2.Value.SelectToken("$.type").Replace(JToken.FromObject(
                //  new
                //  {
                //      type = "lookup",
                //      referenceType = $"{attributeDefinition2.Value.SelectToken("$.type.name")}"
                //  }
                // ));

                attributeDefinition2.Value["type"]["logicalName"] = $"{attributeDefinition2.Value.SelectToken("$.type.name")}".Replace(" ", "").ToLower();
                attributeDefinition2.Value["type"]["schemaName"] = $"{attributeDefinition2.Value.SelectToken("$.type.name")}".Replace(" ", "");
                attributeDefinition2.Value["type"]["collectionSchemaName"] = $"{attributeDefinition2.Value.SelectToken("$.type.pluralName")}".Replace(" ", "");
                attributeDefinition2.Value["type"]["principalColumn"] = entityDefinition.Value.SelectToken("$.logicalName") + "id";
                //  attributeDefinition2.Remove();

            }



            var json = JsonDocument.Parse(jsonraw.ToString(), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip
            });
            Directory.CreateDirectory("obj");
            File.WriteAllText("obj/manifest.g.json", jsonraw.ToString(Newtonsoft.Json.Formatting.Indented));


            ///For loop over jsonraw.selectToken("$.entities")
            ///write a file to obj/specs/<entity.logicalName>.spec.g.json
            ///containing a json schema file for the entity attributes. Sadly there is no strict type map of possible types.
            ///Types can be anything random that i later maps to something in dynamics. (use tolower)
            /// Currently from AttributeTypeCodeConverter - currency,customer,datetime,multilinetext,memo,int,integer,timezone,phone,float,guid,string,text,boolean,bool,
            /// and type.type can be autonumber,choice,picklist,choices,state,status,lookup,string,text

            bool ConvertToSchemaType(JToken attrType, out JToken type)
            {
                type = null;

                var inp = attrType?.ToString();
                if (!(attrType.Type == JTokenType.String))
                {
                    inp = attrType.SelectToken("$.type")?.ToString();
                }

                switch (inp.ToLower())
                {
                    case "binary":
                        type = JToken.FromObject(new
                        {
                            type = "string",
                            contentEncoding = "base64"
                        });
                        return true;
                    case "datetime":
                        type = "datetime";
                        return true;

                    case "customer":
                        return false;
                    case "string":
                    case "text":
                    case "multilinetext":
                        type = "string";
                        return true;
                    case "integer":
                        type = "integer";
                        return true;
                    case "decimal":
                        type = "number";
                        return true;
                    case "boolean":
                        type = "boolean";
                        return true;
                    case "lookup":

                        var foreignTable = jsonraw.SelectToken($"$.entities['{attrType.SelectToken("$.referenceType")}']");
                        var fatAttributes = foreignTable.SelectToken("$.attributes");
                        var fat = fatAttributes.OfType<JProperty>().Where(c => c.Value.SelectToken("$.isPrimaryKey")?.ToObject<bool>() ?? false)
                            .Select(a => a.Value.SelectToken("$.type")).Single();
                        if (fat.Type == JTokenType.Object)
                            fat = fat.SelectToken("$.type");

                        ConvertToSchemaType(fat?.ToString(), out type);

                        type["x-foreign-key"] = JToken.FromObject(new
                        {
                            table = new
                            {
                                logicalName = foreignTable.SelectToken("$.logicalName"),
                                schemaName = foreignTable.SelectToken("$.schemaName"),
                                pluralName = foreignTable.SelectToken("$.pluralName"),
                            },
                            columns = fatAttributes.OfType<JProperty>().Where(c => c.Value.SelectToken("$.isPrimaryKey")?.ToObject<bool>() ?? false)
                            .Select(a => new
                            {
                                logicalName = a.SelectToken("$.logicalName"),
                                schemaName = a.SelectToken("$.schemaName"),

                            })
                        });

                        return true;

                    case "guid":
                        type = JToken.FromObject(new
                        {
                            type = "string",
                            format = "uuid"
                        });
                        return true;
                    case "choices":

                        type = JToken.FromObject(new
                        {
                            type = "array",
                            items = new
                            {
                                type = "integer",
                                @enum = attrType.SelectToken("$.options").OfType<JProperty>().Select(c => c.Value.ToObject<int>())
                            }
                        });
                        return true;
                    case "choice":
                        type = JToken.FromObject(new
                        {
                            type = "integer",
                            @enum = attrType.SelectToken("$.options").OfType<JProperty>().Select(c => c.Value.Type== JTokenType.Object ? c.Value.SelectToken("$.value") : c.Value).Select(v => v.ToObject<int>())
                        });
                        return true;
                    default:
                        throw new NotImplementedException(inp);
                }


            }

            Directory.CreateDirectory("obj/models");
            foreach (var entity in (jsonraw.SelectToken("$.entities") as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                try
                {
                    var entityValue = entity.Value as JObject;
                    var schema = new JObject
                    {
                        ["title"] = entity.Name,
                        ["$schema"] = "http://json-schema.org/draft-07/schema#",
                        ["type"] = "object",
                    };
                    var properties = new JObject();

                    foreach (var attr in (entityValue.SelectToken("$.attributes") as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
                    {
                        var attrValue = attr.Value as JObject;
                        var attrType = attrValue.SelectToken("$.type");



                        var logicalName = attrValue.SelectToken("$.logicalName").ToString();
                        var displayName = attrValue.SelectToken("$.displayName").ToString();

                        var propValues = new JObject();
                        propValues["title"] = displayName;
                        if (!ConvertToSchemaType(attrType, out var type)) continue;
                        propValues["type"] = type;

                        properties[logicalName] = propValues;
                    }

                    schema["properties"] = properties;

                    var filePath = $"obj/models/{entityValue["logicalName"]}.spec.g.json";
                    File.WriteAllText(filePath, schema.ToString(Newtonsoft.Json.Formatting.Indented));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to generate jsonschema for {entity.Name}");
                    Console.Write(ex);
                }
            }

            return json;
        }

        private string TrimId(string v)
        {
            if (string.IsNullOrEmpty(v))
                return v;

            if (v.EndsWith("id", StringComparison.OrdinalIgnoreCase))
                return v.Substring(0, v.Length - 2);

            return v;
        }

        private async Task RunReplacements(JToken jsonraw, string customizationprefix, ILogger logger, JToken elementToRunReplacementFor = null)
        {
            var entityPath = string.Empty;
            var attributePath = string.Empty;
            JToken currentElement = null;
            JToken localelement = null;
            JToken[] localarguments = null;

            var q = new Queue<JToken>(new[] { elementToRunReplacementFor ?? jsonraw });


            var expressionParser = new ExpressionParser<Newtonsoft.Json.Linq.JToken>(
                Options.Create(new ExpressionParserOptions<Newtonsoft.Json.Linq.JToken>() { Document = jsonraw, ThrowOnError = true }), logger,
                new DefaultExpressionFunctionFactory<Newtonsoft.Json.Linq.JToken>()
                {
                    Functions =
                    {
                        ["data"] = (parser,Document,arguments) => {Console.WriteLine(arguments[0]); var child=JToken.Parse(File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path) ,arguments[0]?.ToString())));// q.Enqueue(child);
                                                                                                                                                                                                                                      return Task.FromResult<JToken>(child); },
                        ["customizationprefix"] =(parser,Document,arguments) => Task.FromResult<JToken>(customizationprefix),
                        ["propertyNames"] = (parser,document,arguments) => Task.FromResult<JToken>((arguments[0] is JObject obj ? JToken.FromObject( obj.Properties().Select(k=>k.Name)):new JArray())),
                        ["indexOf"] =(parser,document,arguments) => Task.FromResult<JToken>(Array.IndexOf( arguments[0].ToArray(),arguments[1])),
                        ["default"] = (parser,document,arguments) => Task.FromResult(arguments[0] == null || arguments[0].Type == JTokenType.Null ? arguments[1]:arguments[0]),
                        ["unfoldsource"] = (parser,document,arguments)=>  Task.FromResult(document.SelectToken(arguments[0]["__unroll__path"].ToString())),
                        ["if"] = (parser,document,arguments) => Task.FromResult(arguments[0].ToObject<bool>() ? arguments[1]:arguments[2]),
                        ["condition"] = (parser,document,arguments) => Task.FromResult(arguments[0].ToObject<bool>() ? arguments[1]:arguments[2]),
                        ["in"] =(parser,document,arguments) => Task.FromResult(JToken.FromObject( arguments[1] != null && ( arguments[1] is JObject obj ? obj.ContainsKey(arguments[0].ToString()) :  arguments[1].Any(a=>arguments[0].Equals(a)))  )),
                        ["variables"] = (parser,document,arguments)=> { localarguments= arguments;  return Task.FromResult(jsonraw.SelectToken($"$.variables.{arguments.First()}")?.DeepClone()); },
                        ["concat"] = (parser,document,arguments)=>Task.FromResult<JToken>(string.Join("",arguments.Select(k=>k.ToString())) ),
                        ["entity"] = (parser, document, arguments) =>
                        {
                            var entity = document.SelectToken(entityPath);

                            return Task.FromResult<JToken>(entity);
                        },
                        ["toLogicalName"] = (parser,document,arguments) => Task.FromResult<JToken>(ToSchemaName(arguments[0].ToString()).ToLower()),
                        ["attribute"] = (parser, document, arguments) => Task.FromResult(document.SelectToken(attributePath)),
                        ["attributes"] = (parser, document, arguments) => Task.FromResult(document.SelectToken(entityPath+".attributes")),
                        ["select"] = (parser, document, arguments) => Task.FromResult(arguments.FirstOrDefault(a=>!(a== null || a.Type == JTokenType.Null))),
                        ["propertyName"] = (parser, document, arguments) => Task.FromResult<JToken>( arguments[0].Parent is JProperty prop ? prop.Name : null),
                        ["parent"] =(parser, document, arguments) => Task.FromResult<JToken>(  arguments.Any() ?  (arguments[0].Parent is JProperty prop ? prop.Parent:arguments[0].Parent)  :  (currentElement.Parent is JProperty prop1 ? prop1.Parent:currentElement.Parent)),
                        ["element"]=(parser,document,arguments)=>Task.FromResult(localelement ?? currentElement),
                        ["map"] =async (parser, document, arguments) =>{

                            return JToken.FromObject( await Task.WhenAll( arguments[0].Select(a=>{

                            localelement = a;

                            return parser.EvaluateAsync(arguments[1].ToString());


                            })));

                            }


                    }
                });

            while (q.Count > 0)
            {

                var a = q.Dequeue();
                if (a == null)
                    continue;

                entityPath = ExtractPath(a, "entities");
                attributePath = ExtractPath(a, "attributes") ?? ExtractPath(a, "columns");

                try
                {
                    if (a is JProperty prop)
                    {
                        var value = prop.Value;
                        var str = prop.Name;

                        if (ShouldEvaluate(str))
                        {

                            if (str == "[merge()]")
                            {
                                var parentObj = prop.Parent as JObject;
                                var obj = prop.Value;

                                if (obj.Type == JTokenType.String && ShouldEvaluate(obj.ToString()))
                                {
                                    currentElement = obj;
                                    obj = await EvaluateAsync(expressionParser, obj.ToString());
                                }

                                foreach (var childProp in (obj as JObject).Properties().ToArray())
                                {

                                    childProp.Remove();
                                    parentObj.Add(childProp);
                                    // parentObj.Add(childProp.Name, childProp.Value);
                                    q.Enqueue(childProp);
                                }

                                prop.Remove();
                                continue;
                            }
                            currentElement = prop.Value;
                            var nToken = await EvaluateAsync(expressionParser, str);



                            if (nToken.Type == JTokenType.Null || nToken.Type == JTokenType.Undefined)
                            {
                                prop.Remove();
                                continue;
                            }



                            var nProp = new JProperty(nToken.ToString(), value);
                            prop.Replace(nProp);
                            q.Enqueue(nProp);
                        }
                        else
                        {


                            q.Enqueue(value);
                        }
                    }
                    else if (a is JObject obj)
                    {
                        foreach (var p in obj.Properties())
                        {

                            q.Enqueue(p);


                        }

                    }
                    else if (a is JArray array)
                    {
                        foreach (var element in array)
                            q.Enqueue(element);

                    }
                    else if (a.Type == JTokenType.String)
                    {
                        var str = a.ToString();

                        if (ShouldEvaluate(str))
                        {
                            currentElement = a;
                            var t = await EvaluateAsync(expressionParser, str);

                            a.Replace(t);
                            q.Enqueue(t);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{entityPath}| {attributePath}");
                    throw;
                }
            }
        }

        private static async Task<JToken> EvaluateAsync(ExpressionParser<JToken> expressionParser, string str)
        {
            try
            {
                var nToken = await expressionParser.EvaluateAsync(str);

                if (nToken == null)
                {
                    return nToken;
                }



                if (nToken.Type == JTokenType.Object)
                {
                    var q = new Queue<JToken>();
                    q.Enqueue(nToken);
                    while (q.Count > 0)
                    {
                        var c = q.Dequeue();
                        if (c is JObject o)
                        {
                            foreach (var p in o.Properties())
                                q.Enqueue(p);

                        }
                        else if (c is JProperty p)
                        {
                            if (p.Name.StartsWith("[["))
                            {
                                var nprop = new JProperty(p.Name.Substring(1, p.Name.Length - 2), p.Value);
                                p.Replace(nprop);
                                q.Enqueue(nprop);
                            }
                            else
                            {
                                q.Enqueue(p.Value);
                            }
                        }
                        else if (c is JArray a)
                        {
                            foreach (var e in a)
                                q.Enqueue(e);
                        }
                        else if (c.Type == JTokenType.String && c.ToString().StartsWith("[["))
                        {
                            //  var ch = await expressionParser.EvaluateAsync(c.ToString().Substring(1, c.ToString().Length - 2));
                            //  c.Replace(ch);
                            //  q.Enqueue(ch);
                            var child = c.ToString().Substring(1, c.ToString().Length - 2);
                            // var childToken = await EvaluateAsync(expressionParser, child);
                            c.Replace(child);
                        }
                    }
                }



                while (nToken.Type == JTokenType.String && ShouldEvaluate(nToken.ToString().Substring(1, nToken.ToString().Length - 2)))
                {
                    nToken = await expressionParser.EvaluateAsync(nToken.ToString().Substring(1, nToken.ToString().Length - 2));
                }



                return nToken;
            }
            catch (Exception ex)
            {
                Console.WriteLine("EvaluateAsync");
                throw;
            }
        }

        private static bool ShouldEvaluate(string str)
        {
            return str.StartsWith("[") && str.EndsWith("]") && !str.StartsWith("[[");
        }

        private static string ExtractPath(JToken token, string part)
        {
            string partPath;
            if (token.Path.Contains(part) && !token.Path.EndsWith(part))
            {

                var idx = token.Path.IndexOf(part) + part.Length + 1;

                partPath = new string(token.Path.TakeWhile((c, i) => i < idx || !(c == '.' || c == ']')).ToArray());

                if (partPath.EndsWith('\''))
                    partPath += ']';

            }
            else
            {
                partPath = string.Empty;

            }
            if (partPath.EndsWith("[merge()]"))
            {
                partPath = partPath.Replace("[merge()]", "");
            }
            return string.IsNullOrEmpty(partPath) ? null : partPath;
        }

    }


}
