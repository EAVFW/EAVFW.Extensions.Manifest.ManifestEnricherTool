using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands.Gzip
{
    public class GzipCommand : Command
    {
        [Alias("-u")]
        [Alias("--unzip")]
        [Description("Unzip hex formatted binary stirng ot string")]
        public bool UnzipOption { get; set; }

        [Alias("--zip")]
        [Alias("-z")]
        [Description("Zip string to hex formatted binary string")]
        public bool ZipOption { get; set; }

        [Alias("-p")]
        [Alias("--pretty-print")]
        [Description("Pretty print output")]
        public bool PrettyPrintOption { get; set; }

        [Alias("-o")]
        [Alias("--output")]
        [Description("Output path - defaults to outputting in the terminal")]
        public FileInfo OutputOption { get; set; }

        [Argument]
        [Description("Content to operate on")]
        public string Input { get; set; }

        public GzipCommand() : base("binary", "Work with binary gunzipped data used in EAVFW")
        {
            Handler = COmmandExtensions.Create(this, Array.Empty<Command>(), Run);
        }

        private async Task<int> Run(ParseResult parseResult, IConsole console)
        {
            var (statusCode, output) = await GzipLogic.ConstructString(Input, UnzipOption, ZipOption, PrettyPrintOption);

            if (statusCode != 0 || OutputOption == null)
            {
                console.WriteLine(output);
                return statusCode;
            }

            await WriteToFile(console, output);

            return statusCode;
        }

        private async Task WriteToFile(IConsole console, string output)
        {
            if (OutputOption.Exists)
                console.WriteLine("Output exists and content will be overridden...");

            using var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes(output));
            sourceStream.Seek(0, SeekOrigin.Begin);

            await using var fileStream = new FileStream(OutputOption.FullName, FileMode.Create, FileAccess.Write);
            await sourceStream.CopyToAsync(fileStream);

            await sourceStream.FlushAsync();
            sourceStream.Close();

            console.WriteLine($"Content written to {OutputOption.FullName}");
        }
    }
}
