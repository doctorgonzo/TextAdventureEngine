namespace TextEngine.Tests
{
    using System.Linq;
    using NUnit.Framework;
    using TextEngine;

    // These run against the demo content in Resources — they double as a
    // regression net for the "content missing from the catalogs" class of bug
    // (an item outside Resources silently vanishes from loaded saves).
    public class WorldStateTests
    {
        private WorldState world;

        [OneTimeSetUp]
        public void LoadWorld()
        {
            world = new WorldState();
        }

        [Test]
        public void Catalogs_AreNotEmpty()
        {
            Assert.IsNotEmpty(world.allLocations, "No Location assets found under Resources/Locations");
            Assert.IsNotEmpty(world.allItems, "No Item assets found under Resources/Items");
            Assert.IsNotEmpty(world.allEnemies, "No Enemy assets found under Resources/Enemies");
            Assert.IsNotEmpty(world.allQuests, "No Quest assets found under Resources/Quests");
            Assert.IsNotEmpty(world.allStatusEffects, "No StatusEffect assets found under Resources/Status Effects");
        }

        [Test]
        public void Lookups_FindKnownDemoAssets()
        {
            Assert.IsNotNull(world.FindItemByName("Moonshine"), "Item lookup by asset name failed");
            Assert.IsNotNull(world.FindStatusEffectByName("Poison"), "StatusEffect lookup by asset name failed");
        }

        [Test]
        public void BuildRuntimeState_PopulatesEveryLocation()
        {
            world.BuildRuntimeState();
            foreach (var location in world.allLocations)
            {
                Assert.IsTrue(world.roomItemsState.ContainsKey(location), $"No item state for '{location.name}'");
                Assert.IsTrue(world.roomEnemiesState.ContainsKey(location), $"No enemy state for '{location.name}'");
                Assert.IsTrue(world.roomCharactersState.ContainsKey(location), $"No character state for '{location.name}'");
                Assert.IsTrue(world.roomInteractablesState.ContainsKey(location), $"No interactable state for '{location.name}'");
            }
        }

        // Saves reference content by asset name, so duplicate names silently
        // collide on load. Enforce uniqueness per catalog.
        [Test]
        public void AssetNames_AreUniquePerCatalog()
        {
            AssertUniqueNames(world.allLocations.Select(a => a.name), "Location");
            AssertUniqueNames(world.allItems.Select(a => a.name), "Item");
            AssertUniqueNames(world.allEnemies.Select(a => a.name), "Enemy");
            AssertUniqueNames(world.allQuests.Select(a => a.name), "Quest");
            AssertUniqueNames(world.allStatusEffects.Select(a => a.name), "StatusEffect");
        }

        private static void AssertUniqueNames(System.Collections.Generic.IEnumerable<string> names, string kind)
        {
            var duplicates = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(duplicates, $"Duplicate {kind} asset names break name-keyed saves: {string.Join(", ", duplicates)}");
        }
    }
}
