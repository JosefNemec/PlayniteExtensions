using NUnit.Framework;
using Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniversalSteamMetadata.Tests
{
    [TestFixture]
    public class MetadataTest
    {
        [Test]
        public void StandardDownloadTest()
        {
            var provider = new MetadataProvider(new SteamApiClient());
            var data = provider.GetGameMetadata(578080, BackgroundSource.Image, true);
            Assert.IsNotNull(data.GameInfo);
            Assert.IsNotNull(data.Icon);
            Assert.IsNotNull(data.CoverImage);
            Assert.IsNotNull(data.GameInfo.ReleaseDate);
            Assert.IsNotNull(data.BackgroundImage);
            Assert.IsFalse(string.IsNullOrEmpty(data.GameInfo.Description));
            CollectionAssert.IsNotEmpty(data.GameInfo.Publishers);
            CollectionAssert.IsNotEmpty(data.GameInfo.Developers);
            CollectionAssert.IsNotEmpty(data.GameInfo.Features);
            CollectionAssert.IsNotEmpty(data.GameInfo.Genres);
            CollectionAssert.IsNotEmpty(data.GameInfo.Links);
            CollectionAssert.IsNotEmpty(data.GameInfo.Publishers);
        }

        [Test]
        public void VRMetadataTest()
        {
            var provider = new MetadataProvider(new SteamApiClient());
            var data = provider.GetGameMetadata(378860, BackgroundSource.Banner, true);
            Assert.IsTrue(data.GameInfo.Features.Contains("VR"));
        }
    }
}
