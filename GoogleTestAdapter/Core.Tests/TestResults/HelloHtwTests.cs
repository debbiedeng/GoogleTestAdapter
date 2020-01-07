using GoogleTestAdapter.Helpers;
using GoogleTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static GoogleTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace GoogleTestAdapter.TestResults
{
    [TestClass]
    public class HelloHtwTests : TestsBase
    {

        [TestMethod]
        [TestCategory(Unit)]
        public void DiscoverTests__HalloHtwIsPrinted()
        {
            var discoverer = new GoogleTestDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);
            discoverer.DiscoverTests(TestResources.Tests_DebugX86.Yield(), MockFrameworkReporter.Object);

            MockLogger.Verify(l => l.LogInfo(It.Is<string>(msg => msg == "Hello HTW!")), Times.Once);
        }

    }
}