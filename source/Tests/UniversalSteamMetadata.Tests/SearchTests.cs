using NUnit.Framework;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniversalSteamMetadata.Tests
{
    [TestFixture]
    public class SearchTests
    {
        [Test]
        public void SearchPageParsingTest()
        {
            var results = UniversalSteamMetadata.GetSearchResults("doom");
            CollectionAssert.IsNotEmpty(results);
            Assert.AreNotEqual(0, results[0].GameId);
            Assert.IsNotEmpty(results[0].Description);
            Assert.IsNotEmpty(results[0].Name);
        }

        [Test]
        public void MatchingTest()
        {
            var provider = new UniversalSteamMetadataProvider(null, null);
            Assert.AreEqual(379720, provider.GetMatchingGame(new Game("DOOM")));
            Assert.AreEqual(17480, provider.GetMatchingGame(new Game("Command and Conquer: Red Alert 3")));
            Assert.AreEqual(12210, provider.GetMatchingGame(new Game("Grand Theft Auto 4")));
            Assert.AreEqual(292030, provider.GetMatchingGame(new Game("Witcher 3: Wild Hunt")));
            Assert.AreEqual(292030, provider.GetMatchingGame(new Game("The Witcher 3")));
            Assert.AreEqual(227380, provider.GetMatchingGame(new Game("Dragons Lair")));
            Assert.AreEqual(224940, provider.GetMatchingGame(new Game("Legacy of Kain - Soul Reaver 2")));
            Assert.AreEqual(224940, provider.GetMatchingGame(new Game("Legacy of Kain: Soul Reaver 2")));
            Assert.AreEqual(614570, provider.GetMatchingGame(new Game("Dishonored®: Death of the Outsider™")));
        }
    }
}