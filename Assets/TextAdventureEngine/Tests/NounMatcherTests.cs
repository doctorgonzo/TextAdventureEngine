namespace TextEngine.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using TextEngine;

    public class NounMatcherTests
    {
        private static readonly List<string> Items = new List<string>
        {
            "Brass Lantern", "Blue Key", "Red Key", "Moonshine", "Mead", "Baseball Bat"
        };

        private static string Match(string phrase, out List<string> ambiguous) =>
            NounMatcher.Find(Items, s => s, phrase, out ambiguous);

        [Test]
        public void ExactMatch_IsCaseInsensitive()
        {
            Assert.AreEqual("Moonshine", Match("moonshine", out var ambiguous));
            Assert.IsNull(ambiguous);
        }

        [Test]
        public void PrefixMatch_FindsUniqueItem()
        {
            Assert.AreEqual("Brass Lantern", Match("brass", out var ambiguous));
            Assert.IsNull(ambiguous);
        }

        [Test]
        public void SubstringMatch_FindsUniqueItem()
        {
            Assert.AreEqual("Brass Lantern", Match("lantern", out var ambiguous));
            Assert.IsNull(ambiguous);
        }

        [Test]
        public void AmbiguousMatch_ReturnsCandidateNames()
        {
            Assert.IsNull(Match("key", out var ambiguous));
            Assert.IsNotNull(ambiguous);
            CollectionAssert.AreEquivalent(new[] { "Blue Key", "Red Key" }, ambiguous);
        }

        [Test]
        public void ExactMatch_BeatsAmbiguousPartials()
        {
            // "Mead" is an exact match even though "Mead" is also a prefix
            // of nothing else — and a substring situation must not trigger.
            var items = new List<string> { "Mead", "Mead Barrel" };
            Assert.AreEqual("Mead", NounMatcher.Find(items, s => s, "mead", out var ambiguous));
            Assert.IsNull(ambiguous);
        }

        [Test]
        public void NoMatch_ReturnsDefault()
        {
            Assert.IsNull(Match("dragon", out var ambiguous));
            Assert.IsNull(ambiguous);
        }

        [Test]
        public void EmptyPhraseAndNullSource_AreSafe()
        {
            Assert.IsNull(Match("", out _));
            Assert.IsNull(NounMatcher.Find<string>(null, s => s, "key", out _));
        }

        [Test]
        public void NullEntriesAndNullNames_AreSkipped()
        {
            var items = new List<string> { null, "", "Lantern" };
            Assert.AreEqual("Lantern", NounMatcher.Find(items, s => s, "lantern", out _));
        }

        [Test]
        public void DuplicateNamedObjects_AreNotAmbiguous()
        {
            // Two goblins are the same name — attacking "goblin" should just
            // pick the first, not ask which.
            var items = new List<string> { "Goblin", "Goblin" };
            Assert.AreEqual("Goblin", NounMatcher.Find(items, s => s, "goblin", out var ambiguous));
            Assert.IsNull(ambiguous);
        }
    }
}
