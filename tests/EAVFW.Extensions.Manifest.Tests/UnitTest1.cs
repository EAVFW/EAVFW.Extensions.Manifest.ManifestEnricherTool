using EAVFW.Extensions.Manifest.SDK;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading.Tasks;

namespace EAVFW.Extensions.Manifest.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public async Task TestMethod1()
        {
            var servicecollection = new ServiceCollection();

            servicecollection.AddManifestSDK<DataClientParameterGenerator>();

            var _manifestEnricher = servicecollection.BuildServiceProvider().GetService<IManifestEnricher>();
         
            await _manifestEnricher.LoadJsonDocumentAsync(JToken.Parse(File.ReadAllText("manifestest001.json")), "maas", NullLogger.Instance);



        }
    }
}