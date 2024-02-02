using System.IO;
using System.Threading.Tasks;
using EAVFW.Extensions.Manifest.ManifestEnricherTool;
using JsonDiffPatchDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EAVFW.Extensions.Manifest.Tests.Merge
{
    [TestClass]
    public class AppTest
    {
        [TestMethod]
        public async Task Test1()
        {
            // Arrange
            var rootManifest = new FileInfo("Merge/manifest.json");
            var expectedManifestFileInfo = new FileInfo("Merge/expected.manifest.json");
            var expectedManifest = JToken.Parse(await File.ReadAllTextAsync(expectedManifestFileInfo.FullName));

            var merger = new ManifestMerger(new ModuleMetadataEnricher());

            // Act
            var merged = await merger.MergeManifests(rootManifest);

            await using var fileStream = new FileStream("Merge/outpot.json", FileMode.Create);
            var streamWriter = new StreamWriter(fileStream);
            var jsonWriter = new JsonTextWriter(streamWriter);
            await merged.WriteToAsync(jsonWriter);
            await jsonWriter.FlushAsync();

            // Assert
            var diff = new JsonDiffPatch().Diff(merged, expectedManifest);
            
            Assert.IsNull(diff);
        }
    }
}
