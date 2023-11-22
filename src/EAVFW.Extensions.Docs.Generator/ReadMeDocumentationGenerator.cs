using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public async Task Write(FileInfo outputLocation, string component)
        {
            var writer = new StreamWriter(outputLocation.FullName);

            await WriteTables(writer, component);
            await WriteWizards(writer);
            await WritePlugins(writer);

            await writer.FlushAsync();
        }


        private async Task WriteTables(TextWriter writer, string component)
        {
            await writer.WriteLineAsync("## Tables:\n");

            var t = new DefaultSchemaNameManager();

            var entitiesToWrite = _manifestObject.Entities;

            if (!string.IsNullOrWhiteSpace(component))
            {
                entitiesToWrite = entitiesToWrite.Where(entity =>
                        entity.Value.AdditionalFields.ContainsKey("moduleSource") &&
                        entity.Value.AdditionalFields["moduleSource"].ToString() == component)
                    .ToDictionary(x => x.Key, x => x.Value);
            }

            foreach (var (key, value) in entitiesToWrite)
            {
                _logicalNameLookup[t.ToSchemaName(key)] = key;

                await writer.WriteLineAsync($"### {key}");
                await writer.WriteLineAsync($"Logical name: `{t.ToSchemaName(key)}`");
                await writer.WriteLineAsync($"Plural name: {value.PluralName}");
                await writer.WriteLineAsync($"Description: {value.Description}");
                await writer.WriteLineAsync("Attributes:");
            }
        }

        private async Task WritePlugins(TextWriter writer)
        {
            var groups = _pluginDocumentations.GroupBy(x => x.Entity!.Name);
            await writer.WriteLineAsync("## Plugins: ");
            foreach (var group in groups)
            {
                await writer.WriteLineAsync($"### {group.FirstOrDefault()?.Entity?.Name}");
                foreach (var pluginDocumentation in group)
                {
                    await writer.WriteLineAsync($"#### {pluginDocumentation.Name}");
                    await writer.WriteLineAsync(
                        $"Entity:\t[{pluginDocumentation.Entity?.Name}](#{pluginDocumentation.Entity?.Name})");
                    await writer.WriteLineAsync(
                        $"Context:\t[{pluginDocumentation.Entity?.Name}](#{pluginDocumentation.Entity?.Name})");
                    await writer.WriteLineAsync("Triggers:\n");
                    await writer.WriteLineAsync("| Operation | Execution | Mode | Order |");
                    await writer.WriteLineAsync("|---|---|---|:-:|");
                    foreach (var reg in pluginDocumentation.PluginRegistrations.OrderBy(x => x.Order))
                    {
                        await writer.WriteLineAsync($"|{reg.Operation}|{reg.Execution}|{reg.Mode}|{reg.Order}|");
                    }

                    await writer.WriteLineAsync("\n**Summary:**");

                    await writer.WriteLineAsync(SanitizeSummary(pluginDocumentation.Summary));

                    await writer.WriteLineAsync();
                }
            }
        }

        private async Task WriteWizards(TextWriter writer)
        {
            await writer.WriteLineAsync("## Wizards:");

            foreach (var (key, value) in _wizards.Where(x => x.Value.Wizards.Count > 0))
            {
                await writer.WriteLineAsync($"### {key}");

                foreach (var (wizardKey, wizardDefinition) in value.Wizards)
                {
                    await writer.WriteLineAsync($"#### {wizardKey}");
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
    }
}
