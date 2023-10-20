using System.Threading.Tasks;
using EAVFW.Extensions.Manifest.ManifestEnricherTool.Commands.Gzip;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EAVFW.Extensions.Manifest.Tests.Gzip
{
    [TestClass]
    public class GzipLogicTest
    {
        [TestMethod]
        public async Task NoOptions()
        {
            // Arrange
            var jsonString = "{ \"value\": 2 }";
            var expectedStatusCode = 127;

            // Act
            var (statusCode, output) = await GzipLogic.ConstructString(jsonString);

            // Assert
            Assert.AreEqual(expectedStatusCode, statusCode);
        }


        [TestMethod]
        public async Task ZipAndUnZipTests()
        {
            // Arrange
            var jsonString = "{ \"value\": 2 }";
            var expectedStatusCode = 0;

            // Act
            var (statusCode, output) =
                await GzipLogic.ConstructString((await GzipLogic.ConstructString(jsonString, zip: true)).Item2,
                    unzip: true);

            // Assert
            Assert.AreEqual(expectedStatusCode, statusCode);
            Assert.AreEqual(jsonString, output);
        }

        [TestMethod]
        public async Task ZipAndUnZipWithPrettyPrintTests()
        {
            // Arrange
            var jsonString = "{ \"value\": 2 }";
            var prettyfied = "{\n  \"value\": 2\n}";
            var expectedStatusCode = 0;

            // Act
            var (statusCode, output) = await GzipLogic.ConstructString(
                (await GzipLogic.ConstructString(jsonString, zip: true)).Item2, unzip: true, prettyPrint: true);

            // Assert
            Assert.AreEqual(expectedStatusCode, statusCode);
            Assert.AreEqual(prettyfied, output);
        }

        [TestMethod]
        public async Task UnZipEavfwGeneratedContent()
        {
            // Arrange
            var zippedString =
                "0x1F8B08000000000000139C51414EC33010FC8BAFD4C8719C36C9AD824BA50252E90D71D8D8EBC852EA147B73A8A2FE1D3B05CE153E8DAC99D99DD999812637FAC8DA993D0504C2FDDBEE19080EF83561A4178C117ADC2EAC4C72FE3C5106C3E8C28DC35A06CA182937C0D142C795EE2A5EA2042E6AD369B0AAAA9A82AD5840EDCE0E3DA5791FACA884EAACB45CDB3572D5E8861B5B36BFA235405DB3CFEB8A45029A9284BD4F5AA34193ACBAD15CEE1F6CC10D680E0831A7F0D3305C932F05D7F71896F0C71BCE90DC0993B514B2E485E0853C8A4DAB8A56C9C7B2484B370F42B442FC2D31B394C8D1E51516DD1E89308CD67983279FFE7FAA8CB702C66076E6FECD4DBA451EF1AFEAF2FB060000FFFF";
            var unzippedString =
                "{\"actions\":{\"CreateLOIDataRequestMessageAction\":{\"input\":{\"loirequest\":\"a4dd227a-efab-4cb5-3e2a-08dbcaf45591\",\"recipients\":[\"1504bf2f-cf6e-49c9-df39-08dbcaf6aa88\"]},\"status\":\"Succeded\",\"body\":\"a4dd227a-efab-4cb5-3e2a-08dbcaf45591\",\"failedReason\":null}},\"triggers\":{\"Trigger\":{\"time\":\"2023-10-12T07:41:42.311509+00:00\",\"body\":{\"entityName\":\"LetterofindemnityRequests\",\"recordId\":\"a4dd227a-efab-4cb5-3e2a-08dbcaf45591\",\"data\":{\"recipients\":[\"1504bf2f-cf6e-49c9-df39-08dbcaf6aa88\"]}}}}}";

            var expectedStatusCode = 0;

            // Act
            var (statusCode, output) = await GzipLogic.ConstructString(zippedString, unzip: true);

            // Asset
            Assert.AreEqual(expectedStatusCode, statusCode);
            Assert.AreEqual(unzippedString, output);
        }
    }
}