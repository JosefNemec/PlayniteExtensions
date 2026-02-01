using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlayniteExtensions.Common;

namespace Common.Tests
{
    [TestFixture]
    public class GameNameMatcherTests
    {
        [Test]
        public void ToGameKey_ResultTests()
        {
            void AssertMatch(string a, string b)
            {
                Assert.AreEqual(a, GameNameMatcher.ToGameKey(b), $"Expected match: \"{a}\" <-> \"{b}\"");
            }

            // Basic normalization
            AssertMatch("thewitcher3", "Witcher 3, The");
            AssertMatch("thewitcher3", "The Witcher 3");

            // Case insensitivity
            AssertMatch("nierautomata", "NieR: Automata");
            AssertMatch("nierautomata", "NIER: AUTOMATA");

            // Special characters are removed
            AssertMatch("nierautomata", "NieR: Automata™");
            AssertMatch("finalfantasyviiremake", "Final Fantasy VII: Remake!?$%$");

            // Trailing bracketed metadata is removed
            AssertMatch("nierautomata", "NieR: Automata [PC]");
            AssertMatch("nierautomata", "NieR: Automata (Steam)");

            // Mix of previous cases
            AssertMatch("thewitcher3", "Witcher 3, The (GOTY Edition)");
            AssertMatch("thelegendofzeldabreathofthewild", "Legend of Zelda, The: Breath of the Wild");

            // Empty and null handling
            AssertMatch(string.Empty, "");
            AssertMatch(string.Empty, ((string)null));

            // Digits are preserved
            AssertMatch("ff7", "FF7");
        }

        [Test]
        public void ToGameKey_MatchingEquivalenceTests()
        {
            void AssertMatch(string a, string b)
            {
                Assert.AreEqual(GameNameMatcher.ToGameKey(a), GameNameMatcher.ToGameKey(b), $"Expected match: \"{a}\" <-> \"{b}\"");
            }

            void AssertNotMatch(string a, string b)
            {
                Assert.AreNotEqual(GameNameMatcher.ToGameKey(a), GameNameMatcher.ToGameKey(b), $"Expected not match: \"{a}\" <-> \"{b}\"");
            }

            // General cases
            AssertMatch("Middle-earth™: Shadow of War™", "Middle-earth: Shadow of War");
            AssertMatch("Command®   & Conquer™ Red_Alert 3™ : Uprising©:_Best Game", "Command & Conquer Red Alert 3: Uprising: Best Game");
            AssertMatch("Pokemon.Red.[US].[l33th4xor].Test.[22]", "Pokemon Red Test");
            AssertMatch("Pokemon.Red.[US].(l33th 4xor).Test.(22)", "Pokemon Red Test");
            AssertMatch("[PROTOTYPE]™", "[PROTOTYPE]");
            AssertMatch("(PROTOTYPE2)™", "(PROTOTYPE2)");

            // Articles
            AssertMatch("Witcher 3, The", "The Witcher 3");
            AssertMatch("Legend of Zelda, The: Breath of the Wild", "The Legend of Zelda: Breath of the Wild");

            // Platform / store metadata
            AssertMatch("NieR: Automata", "NieR: Automata [PC]");
            AssertMatch("NieR: Automata", "NieR: Automata (Steam)");
            AssertMatch("DOOM", "DOOM (2016)");
            AssertMatch("Final Fantasy VII Remake", "Final Fantasy VII Remake [PC]");

            // Special characters & punctuation
            AssertMatch("NieR: Automata™", "NieR - Automata");
            AssertMatch("Dragon's Dogma", "Dragons Dogma");

            // Case differences
            AssertMatch("nier automata", "NieR: Automata");
            AssertMatch("FINAL FANTASY X", "Final Fantasy X");

            // Whitespace & formatting
            AssertMatch("The Witcher 3", "   The   Witcher   3   ");
            AssertMatch("Dark Souls III", "Dark   Souls   III");

            // Edition words should not match unless you want them to
            AssertNotMatch("Persona 5", "Persona 5 Royal");

            // Numbers written differently will not match
            AssertNotMatch("Final Fantasy VII", "Final Fantasy 7");

            // Year metadata
            AssertMatch("Resident Evil 4", "Resident Evil 4 (2005)");

            // Region tags
            AssertMatch("Silent Hill 2", "Silent Hill 2 [USA]");
        }
    }
}
