using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EAVFW.Extensions.Docs.Extracter;
using Microsoft.AspNetCore.DataProtection;
using Sprache;

namespace EAVFW.Extensions.Docs.Generator
{
    public class PluginDocumentationToReadMe
    {
        public async Task WriteReadMe(IEnumerable<PluginDocumentation> pluginDocumentations)
        {
            await using var writer = new StreamWriter("documentation.md");

            var groups = pluginDocumentations.GroupBy(x => x.Entity!.Name);
            await writer.WriteLineAsync("# Plugins ");
            foreach (var group in groups)
            {
                await writer.WriteLineAsync($"## {group.FirstOrDefault()?.Entity?.Name}");
                foreach (var pluginDocumentation in group)
                {
                    await writer.WriteLineAsync($"### {pluginDocumentation.Name}");
                    await writer.WriteLineAsync(
                        $"Entity:\t[{pluginDocumentation.Entity?.Name}](#{pluginDocumentation.Entity?.Name})");
                    await writer.WriteLineAsync(
                        $"Context:\t[{pluginDocumentation.Entity?.Name}](#{pluginDocumentation.Entity?.Name})");
                    await writer.WriteLineAsync("Triggers:");
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

        private static string SanitizeSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary)) return summary;

            var lines = summary.Split("\n").Select(x => x);

            lines = lines.Where(x => !string.IsNullOrWhiteSpace(x));
            lines = lines.Select(x => x.Trim());
            lines = lines.Select(x => ToMarkDownLink.Parse(x));
            

            return string.Join("", lines);
        }

        private static readonly Parser<string> Text = Parse.CharExcept('<').Many().Text();

        private static readonly Parser<string> Value =
            Parse.AnyChar.Except(Parse.Char('\"')).Many().Text();
        
        private static readonly Parser<KeyValuePair<string, string>> Property =
            from key in Parse.LetterOrDigit.Or(Parse.Char('-')).Many().Text()
            from eq in Parse.Char('=')
            from value in Parse.AnyChar.Except(Parse.Char('\"')).Many().Contained(Parse.Char('\"'), Parse.Char('\"')).Text()
            select new KeyValuePair<string, string>(key, value);

        private static readonly Parser<Dictionary<string, string>> Properties =
            from properties in Property.DelimitedBy(Parse.Char(' '))
            select properties.ToDictionary(x => x.Key, x => x.Value);

        private static readonly Parser<string> LinkReplace =
            from s in Parse.String("<see")
            from _ in Parse.WhiteSpace.Many()
            from props in Properties
            from __ in Parse.WhiteSpace.Many()
            from d in Parse.String("/>")
            select $"[{props["cref"]}](#{props["cref"]})";

        private static readonly Parser<string> ToMarkDownLink =
            from s in Text.Or(LinkReplace).Many()
            select string.Join("", s);
        
        
    }
}
