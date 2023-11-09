using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EAVFW.Extensions.Docs.Extracter
{
    public class Entity
    {
        [JsonPropertyName("wizards")] public Dictionary<string, Wizard> Wizards { get; set; }
    }

    public class Wizard
    {
        [JsonPropertyName("title")] public string Title { get; set; }

        [JsonPropertyName("triggers")] public JsonElement Triggers { get; set; }

        [JsonPropertyName("tabs")] public Dictionary<string, Tab> Tabs { get; set; }
    }

    public struct Trigger
    {
        [JsonPropertyName("form")] public string Form { get; set; }

        [JsonPropertyName("ribbon")] public string Ribbon { get; set; }
    }

    [JsonConverter(typeof(TabConverter))]
    public class Tab
    {
        [JsonPropertyName("columns")] public JsonElement Columns { get; set; }
        
        [JsonPropertyName("visible")] public StringOrBoolean StringOrBoolean { get; set; }
        
        [JsonPropertyName("onTransitionOut")] public Transition? OnTransitionOut { get; set; }

        [JsonPropertyName("onTransitionIn")] public Transition? OnTransitionIn { get; set; }

        [JsonPropertyName("actions")] public JsonElement? Actions { get; set; } // TODO: This also needs to be enriched
    }

    public struct StringOrBoolean
    {
        public StringOrBoolean(string stringValue)
        {
            StringValue = stringValue;
            IsBool = false;
            BooleanValue = default;
        }

        public StringOrBoolean(bool booleanValue)
        {
            StringValue = default;
            IsBool = false;
            BooleanValue = booleanValue;
        }

        public string? StringValue { get; }

        public bool BooleanValue { get; }

        public bool IsBool { get; }
    }


    public class Transition
    {
        [JsonPropertyName("workflow")] public string? Workflow { get; set; }

        [JsonPropertyName("workflowSummary")] public string? WorkflowSummary { get; set; }

        [JsonPropertyName("message")] public Message? Message { get; set; }
    }

    public class Message
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
    }
}
