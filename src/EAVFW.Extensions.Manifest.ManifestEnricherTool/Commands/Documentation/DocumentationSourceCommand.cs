using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using EAVFW.Extensions.Docs.Extractor;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands.Documentation
{
    public class DocumentationSourceCommand : Command
    {
        public DocumentationSourceCommand(IDocumentLogic documentLogic) : base("docs", "Work with documentation")
        {
            Handler = COmmandExtensions.Create(this, Array.Empty<Command>(), Run);

            AddCommand(new DocumentationSourceExtractorCommand(documentLogic));
            AddCommand(new DocumentationGeneratorCommand());
        }

        private Task<int> Run(ParseResult parseResult, IConsole console)
        {
            return Task.FromResult(0);
        }
    }
}
