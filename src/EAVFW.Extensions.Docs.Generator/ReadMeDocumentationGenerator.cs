using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EAVFW.Extensions.Docs.Extractor;
using EAVFW.Extensions.Manifest.SDK;

namespace EAVFW.Extensions.Docs.Generator
{
    public class ReadMeDocumentationGenerator : IDocumentationGenerator
    {
        private readonly TransformXmlTag _transformXmlTag = new();
        private readonly Dictionary<string, string> _logicalNameLookup = new();
        private Dictionary<string, EntityDefinition> _wizards;
        private IEnumerable<PluginDocumentation> _pluginDocumentations;
        private ManifestDefinition _manifestObject;

        private ISchemaNameManager _schemaNameManager = new DefaultSchemaNameManager();


        public void AddPluginSource(IEnumerable<PluginDocumentation> pluginDocumentations)
        {
            _pluginDocumentations = pluginDocumentations;
        }


        public void AddWizardSource(Dictionary<string, EntityDefinition> entitiesWithWizards)
        {
            _wizards = entitiesWithWizards;
        }

        public void AddGeneratedManifest(ManifestDefinition generatedManifest)
        {
            _manifestObject = generatedManifest;
        }

        private string BuildAttributeDescription(AttributeObjectDefinition attribute, string key)
        {
            var s = "";
            var _component = "manifest.loi.json";
            if (attribute.AttributeType.Type == "lookup")
            {
                var lookup = attribute.AttributeType.ReferenceType;
                _manifestObject.Entities.FirstOrDefault(x => x.Key == lookup).Value.AdditionalFields
                    .TryGetValue("moduleSource", out var source);

                if (source.ToString() == _component)
                {
                    s +=
                        $"Lookup: [{attribute.AttributeType.ReferenceType}](#{_schemaNameManager.ToSchemaName(attribute.AttributeType.ReferenceType)})<br/>";
                }
                else
                {
                    s += $"Lookup: {attribute.AttributeType.ReferenceType} ({source})<br/>";
                }
            }

            if (attribute.AttributeType.Type == "Choice")
            {
                var defaultOption = attribute.AdditionalFields.TryGetValue("default", out var _default);
                var defaultValue = 0;
                if (defaultOption)
                {
                    // defaultValue = _default.GetInt32();
                }


                s += "Options: <br/>";
                foreach (var (key1, value) in attribute.AttributeType.Options)
                {
                    s += $"- {value}: {key1}";

                    if (defaultOption && value.GetInt32() == defaultValue)
                    {
                        s += " (default)";
                    }

                    s += "<br/>";
                }
            }

            return s + $"_Display name:_ {key}";
        }

        public async Task Write(FileInfo outputLocation, string component)
        {
            var writer = new StreamWriter(outputLocation.FullName);

            await writer.WriteLineAsync($"# Documentation for {component}");

            var entitiesToWrite = _manifestObject.Entities;

            if (!string.IsNullOrWhiteSpace(component))
            {
                entitiesToWrite = entitiesToWrite.Where(entity =>
                        entity.Value.AdditionalFields.ContainsKey("moduleSource") &&
                        entity.Value.AdditionalFields["moduleSource"].ToString() == component)
                    .ToDictionary(x => x.Key, x => x.Value);
            }

            await writer.WriteLineAsync("## Table of contents");

            var index = 2;
            await writer.WriteLineAsync("1. [Class diagram](#Class-diagram)");
            foreach (var (key, value) in entitiesToWrite)
            {
                await writer.WriteLineAsync($"{index++}. [{key}](#{key.Replace(' ', '-')})");
            }

            await writer.WriteLineAsync("# Class diagram");
            await writer.WriteAsync(EntitiesToClassDiagram(_manifestObject, component));

            var ignored = new List<string>
                { "Modified On", "Modified By", "Created By", "Created On", "Row Version", "Owner" };

            foreach (var (key, value) in entitiesToWrite)
            {
                await writer.WriteLineAsync($"## {key}");

                await writer.WriteLineAsync(value.Description);

                await WriteAttributes(writer, value, ignored);

                await WritePlugins(writer, key);


                await WriteWizards(writer, key);
            }

            await writer.FlushAsync();
        }

        private async Task WriteWizards(StreamWriter writer, string key)
        {
            await writer.WriteLineAsync("### Wizards");

            if (_wizards.TryGetValue(key, out var wizard))
            {
                foreach (var (s, wizardDefinition) in wizard.Wizards)
                {
                    await writer.WriteLineAsync($"#### {wizardDefinition.Title}");

                    await writer.WriteLineAsync("\nTriggers:\n");
                    await writer.WriteLineAsync("| Type | Value |");
                    await writer.WriteLineAsync("|------|-------|");

                    foreach (var (k, triggerDefinition) in wizardDefinition.Triggers)
                    {
                        if (string.IsNullOrWhiteSpace(triggerDefinition.Form))
                            await writer.WriteLineAsync($"| Ribbon | {triggerDefinition.Ribbon} |");
                        else
                            await writer.WriteLineAsync($"| Form | {triggerDefinition.Form} |");
                    }

                    await writer.WriteLineAsync("\nTabs:\n");
                    await writer.WriteLineAsync("| Tab | Visible | OnTransitionIn | OnTransitionOut |");
                    await writer.WriteLineAsync("| -- | -- | -- | -- |");

                    foreach (var (key1, tabDefinition) in wizardDefinition.Tabs)
                    {
                        await writer.WriteLineAsync(
                            $"| {key1} | {GetVisibleString(tabDefinition.Visible.ToString())} |  {GetTransitionString(tabDefinition.OnTransitionIn)} | {GetTransitionString(tabDefinition.OnTransitionOut)} |");
                    }
                }
            }
            else
            {
                await writer.WriteLineAsync("_No wizards_");
            }
        }

        private string GetVisibleString(string visible)
        {
            return string.IsNullOrWhiteSpace(visible) ? "" : $"`{visible}`";
        }

        private string GetTransitionString(TransitionDefinition transitionDefinition)
        {
            if (!string.IsNullOrWhiteSpace(transitionDefinition?.Workflow))
            {
                var summaryString = "";
                if (transitionDefinition.AdditionalData.TryGetValue("x-workflowSummary", out var summary) &&
                    summary != null)
                {
                    summaryString = $"<br/> Summary: {SanitizeSummary(summary.ToString())}";
                }

                return $"{transitionDefinition.Workflow} {summaryString}";
            }

            return "";
        }

        private async Task WritePlugins(StreamWriter writer, string key)
        {
            await writer.WriteLineAsync("### Plugins");

            var plugins = _pluginDocumentations.Where(x => x.Entity.Name == _schemaNameManager.ToSchemaName(key))
                .ToList();
            if (!plugins.Any()) await writer.WriteLineAsync("_No plugins_");
            foreach (var pluginDocumentation in plugins)
            {
                await writer.WriteLineAsync($"#### {pluginDocumentation.Name}");

                await writer.WriteLineAsync(SanitizeSummary(pluginDocumentation.Summary));

                await writer.WriteLineAsync();

                await writer.WriteLineAsync(
                    $"Entity:\t[{pluginDocumentation.Entity?.Name}](#{pluginDocumentation.Entity?.Name})");
                await writer.WriteLineAsync(
                    $"Context:\t[{pluginDocumentation.Entity?.Name}](#{pluginDocumentation.Entity?.Name})");

                await writer.WriteLineAsync();
                await writer.WriteLineAsync("| Operation | Execution | Mode | Order |");
                await writer.WriteLineAsync("|---|---|---|:-:|");
                foreach (var reg in pluginDocumentation.PluginRegistrations.OrderBy(x => x.Order))
                {
                    await writer.WriteLineAsync($"|{reg.Operation}|{reg.Execution}|{reg.Mode}|{reg.Order}|");
                }
            }
        }

        private async Task WriteAttributes(StreamWriter writer, EntityDefinition value, List<string> ignored)
        {
            await writer.WriteLineAsync("### Attributes");

            await writer.WriteLineAsync("| Name | Type | Details |");
            await writer.WriteLineAsync("|------|------|---------|");

            foreach (var (s, attributeBase) in value.Attributes.Where(x => !ignored.Contains(x.Key)))
            {
                if (attributeBase is AttributeObjectDefinition attributeDefinition)
                {
                    await writer.WriteLineAsync(
                        $"| {_schemaNameManager.ToSchemaName(s)} | {attributeDefinition.AttributeType.Type} | {BuildAttributeDescription(attributeDefinition, s)} |");
                }
            }
        }

        private string SanitizeSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary)) return summary;

            var lines = summary.Split("\n").Select(x => x);

            lines = lines.Where(x => !string.IsNullOrWhiteSpace(x));
            lines = lines.Select(x => x.Trim());
            lines = lines.Select(x => _transformXmlTag.TransformString(x, TransformTag));

            return string.Join(" ", lines);
        }

        /// <summary>
        /// Function i expression motoren
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        private string TransformTag(string tag, Dictionary<string, string> properties)
        {
            if (tag != "see")
                return tag;

            var target = properties["cref"];

            var key = target.Split(':').Last().Split('.').Last();

            _logicalNameLookup.TryGetValue(key, out var value);

            return $"[{value ?? key}](#{key})";
        }

        private string EntitiesToClassDiagram(ManifestDefinition manifest, string component)
        {
            var diagramBuilder = new StringBuilder();

            diagramBuilder.AppendLine("::: mermaid");
            diagramBuilder.AppendLine("classDiagram");

            var t = new DefaultSchemaNameManager();

            var entities = manifest.Entities;
            if (!string.IsNullOrWhiteSpace(component))
            {
                entities = manifest.Entities.Where(entity =>
                        entity.Value.AdditionalFields.ContainsKey("moduleSource") &&
                        entity.Value.AdditionalFields["moduleSource"].ToString() == component)
                    .ToDictionary(x => x.Key, x => x.Value);
            }

            var ignored = new List<string>
                { "Modified On", "Modified By", "Created By", "Created On", "Row Version", "Owner" };

            foreach (var (key, value) in entities)
            {
                var attributes = value.Attributes.Where(x => !ignored.Contains(x.Key)).ToList();

                diagramBuilder.AppendLine($"\tclass {t.ToSchemaName(key)}{{");
                foreach (var (s, attributeDefinitionBase) in attributes)
                {
                    if (attributeDefinitionBase is AttributeObjectDefinition o)
                    {
                        // Argmunt
                        if (o.AttributeType.Type == "lookup")
                        {
                            diagramBuilder.AppendLine($"\t\t+{t.ToSchemaName(o.AttributeType.ReferenceType)} {s}");
                        }
                        else
                        {
                            diagramBuilder.AppendLine($"\t\t+{o.AttributeType.Type} {s}");
                        }
                    }
                }

                diagramBuilder.AppendLine("\t}");

                var relations = new HashSet<string>();
                foreach (var (_, attributeDefinitionBase) in attributes)
                {
                    if (attributeDefinitionBase is AttributeObjectDefinition o && o.AttributeType.Type == "lookup")
                    {
                        relations.Add($"\t{o.AttributeType.ReferenceType} <-- {key}");
                    }
                }

                foreach (var relation in relations)
                {
                    diagramBuilder.AppendLine(relation);
                }
            }

            diagramBuilder.AppendLine(":::");
            return diagramBuilder.ToString();
        }
    }
}
