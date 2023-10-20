using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands.Gzip
{
    public class GzipLogic
    {
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
            if (str.StartsWith("0x"))
                str = str[2..];
            
            var bytes = Convert.FromHexString(str);
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