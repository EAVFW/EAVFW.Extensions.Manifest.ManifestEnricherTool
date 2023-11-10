using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EAVFW.Extensions.Docs.Extracter;
using EAVFW.Extensions.Manifest.SDK;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace EAVFW.Extensions.Docs.Generator
{
    public class PluginDocumentationToReadMe : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly Dictionary<string, string> _logicalNameLookup = new();
        private readonly TransformXmlTag _transformXmlTag;

        public PluginDocumentationToReadMe(
            string path = "/Users/thyge/Documents/Obsidian Vault/Delegate Lava-Stone/Delegate/documentation.md")
        {
            _transformXmlTag = new TransformXmlTag();
            _writer = new StreamWriter(path);
        }

        public async Task WriteTables(FileInfo manifest)
        {
            var manifestObject = await JsonSerializer.DeserializeAsync<ManifestDefinition>(manifest.OpenRead());

            await _writer.WriteLineAsync("## Tables:\n");

            var t = new DefaultSchemaNameManager();

            Debug.Assert(manifestObject != null, nameof(manifestObject) + " != null");
            foreach (var (key, value) in manifestObject.Entities)
            {
                _logicalNameLookup[t.ToSchemaName(key)] = key;

                await _writer.WriteLineAsync($"### {key}");
                await _writer.WriteLineAsync($"Logical name: `{t.ToSchemaName(key)}`");
                await _writer.WriteLineAsync($"Plural name: {value.PluralName}");
                await _writer.WriteLineAsync($"Description: {value.Description}");
                await _writer.WriteLineAsync("Attributes:");
            }
        }

        public async Task WritePlugins(IEnumerable<PluginDocumentation> pluginDocumentations)
        {
            var groups = pluginDocumentations.GroupBy(x => x.Entity!.Name);
            await _writer.WriteLineAsync("## Plugins ");
            foreach (var group in groups)
            {
                await _writer.WriteLineAsync($"### {group.FirstOrDefault()?.Entity?.Name}");
                foreach (var pluginDocumentation in group)
                {
                    await _writer.WriteLineAsync($"#### {pluginDocumentation.Name}");
                    await _writer.WriteLineAsync(
                        $"Entity:\t[{pluginDocumentation.Entity?.Name}](#{pluginDocumentation.Entity?.Name})");
                    await _writer.WriteLineAsync(
                        $"Context:\t[{pluginDocumentation.Entity?.Name}](#{pluginDocumentation.Entity?.Name})");
                    await _writer.WriteLineAsync("Triggers:\n");
                    await _writer.WriteLineAsync("| Operation | Execution | Mode | Order |");
                    await _writer.WriteLineAsync("|---|---|---|:-:|");
                    foreach (var reg in pluginDocumentation.PluginRegistrations.OrderBy(x => x.Order))
                    {
                        await _writer.WriteLineAsync($"|{reg.Operation}|{reg.Execution}|{reg.Mode}|{reg.Order}|");
                    }

                    await _writer.WriteLineAsync("\n**Summary:**");

                    await _writer.WriteLineAsync(SanitizeSummary(pluginDocumentation.Summary));

                    await _writer.WriteLineAsync();
                }
            }
        }
        
        // public async Task WriteWizards()
            

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


        public async void Dispose()
        {
            await _writer.FlushAsync();
            await _writer.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
