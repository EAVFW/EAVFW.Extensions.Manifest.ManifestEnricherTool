using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands
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
            var (statusCode, output) = await ConstructString(Input, UnzipOption, ZipOption, PrettyPrintOption);

            if (statusCode != 0)
            {
                console.WriteLine(output);
                return statusCode;
            }

            if (OutputOption == null)
            {
                console.WriteLine(output);

                return statusCode;
            }

            if (OutputOption.Exists)
                console.WriteLine("Output exists and content will be overridden...");

            using var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes(output));
            sourceStream.Seek(0, SeekOrigin.Begin);

            await using var fileStream = new FileStream(OutputOption.FullName, FileMode.Create, FileAccess.Write);
            await sourceStream.CopyToAsync(fileStream);

            await sourceStream.FlushAsync();
            sourceStream.Close();

            console.WriteLine($"Content written to {OutputOption.FullName}");

            return statusCode;
        }
        public static async Task<(int, string)> ConstructString(
            string input,
            bool unzip = false,
            bool zip = false,
            bool prettyPrint = false)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new Exception("Cannot operate on empty input");
            }

            if (unzip)
            {
                var unzipped = await Unzip(input);

                if (!prettyPrint) return (0, unzipped);

                var prettified = await FormatJson(unzipped);
                return (0, prettified);
            }

            if (zip)
            {
                var zipped = await Zip(input);
                return (0, zipped);
            }

            return (127, "Command not found");
        }

        internal static async Task<string> Unzip(string str)
        {
            var bytes = Convert.FromHexString(str[2..]);
            using var tinyStream =
                new StreamReader(new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress));
            return await tinyStream.ReadToEndAsync();
        }

        internal static async Task<string> Zip(string str)
        {
            using var memoryStream = new MemoryStream();
            await using var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress);
            await using var writer = new StreamWriter(gZipStream, Encoding.UTF8);
            await writer.WriteAsync(str);
            await writer.FlushAsync();

            var compressedBytes = memoryStream.ToArray();

            return Convert.ToHexString(compressedBytes);
        }

        internal static Task<string> FormatJson(string str)
        {
            using var jDoc = JsonDocument.Parse(str);
            return Task.FromResult(JsonSerializer.Serialize(jDoc, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
