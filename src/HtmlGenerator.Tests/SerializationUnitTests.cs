using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.SourceBrowser.HtmlGenerator.Tests
{
    [TestClass]
    public class SerializationUnitTests
    {
        //todo: Move to text utility tests. Not in serialization any more
        [TestMethod]
        public void TestULongToHexStringRoundtrip()
        {
            for (int i = 0; i < 1000; i++)
            {
                var originalStringId = Paths.GetMD5Hash(i.ToString(), 16);
                var id = Paths.GetMD5HashULong(i.ToString(), 16);
                var stringId = Common.TextUtilities.ULongToHexString(id);
                Assert.AreEqual(originalStringId, stringId);
                Assert.AreEqual(16, stringId.Length);
                var actualId = Common.TextUtilities.HexStringToULong(stringId);
                Assert.AreEqual(id, actualId);
            }
        }
    }
}
