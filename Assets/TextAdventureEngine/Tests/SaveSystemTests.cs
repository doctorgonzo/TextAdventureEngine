namespace TextEngine.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using NUnit.Framework;
    using TextEngine;
    using UnityEngine;

    public class SaveSystemTests
    {
        private const string TestFileName = "textengine_test_save.json";
        private string TestPath => Path.Combine(Application.persistentDataPath, TestFileName);

        [TearDown]
        public void DeleteTestFile()
        {
            if (File.Exists(TestPath)) File.Delete(TestPath);
        }

        [Test]
        public void Read_ReturnsNullWhenNoFileExists()
        {
            var system = new SaveSystem(TestFileName);
            Assert.IsFalse(system.SaveExists());
            Assert.IsNull(system.Read());
        }

        [Test]
        public void WriteThenRead_RoundTripsEveryFieldGroup()
        {
            var system = new SaveSystem(TestFileName);
            var data = new SaveData
            {
                playerName = "Tester",
                playerDrunkenness = 0.25f,
                playerCurrentHealth = 42,
                currentLocationName = "First Room",
                equippedWeaponName = "Baseball Bat",
            };
            data.playerInventoryItemNames.Add("Moonshine");
            data.learnedSkillNames.Add("Fireball");
            data.activeQuests.Add(new ActiveQuestSaveState
            {
                questName = "Test",
                objectivesCompleted = new List<bool> { true, false }
            });
            data.completedQuestNames.Add("Prologue");
            data.activeStatusEffects.Add(new StatusEffectSaveState
            {
                effectName = "Poison",
                remainingTime = 3.5f
            });
            data.trueFlags.Add("DrankMead");
            data.interactableStates.Add(new InteractableState
            {
                locationName = "First Room",
                interactableName = "Lever",
                state = "pulled"
            });

            system.Write(data);
            Assert.IsTrue(system.SaveExists());
            var loaded = system.Read();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(SaveData.CurrentVersion, loaded.version);
            Assert.AreEqual("Tester", loaded.playerName);
            Assert.AreEqual(0.25f, loaded.playerDrunkenness);
            Assert.AreEqual(42, loaded.playerCurrentHealth);
            Assert.AreEqual("First Room", loaded.currentLocationName);
            Assert.AreEqual("Baseball Bat", loaded.equippedWeaponName);
            CollectionAssert.AreEqual(new[] { "Moonshine" }, loaded.playerInventoryItemNames);
            CollectionAssert.AreEqual(new[] { "Fireball" }, loaded.learnedSkillNames);
            Assert.AreEqual(1, loaded.activeQuests.Count);
            Assert.AreEqual("Test", loaded.activeQuests[0].questName);
            CollectionAssert.AreEqual(new[] { true, false }, loaded.activeQuests[0].objectivesCompleted);
            CollectionAssert.AreEqual(new[] { "Prologue" }, loaded.completedQuestNames);
            Assert.AreEqual(1, loaded.activeStatusEffects.Count);
            Assert.AreEqual("Poison", loaded.activeStatusEffects[0].effectName);
            Assert.AreEqual(3.5f, loaded.activeStatusEffects[0].remainingTime);
            CollectionAssert.AreEqual(new[] { "DrankMead" }, loaded.trueFlags);
            Assert.AreEqual(1, loaded.interactableStates.Count);
            Assert.AreEqual("First Room", loaded.interactableStates[0].locationName);
            Assert.AreEqual("Lever", loaded.interactableStates[0].interactableName);
            Assert.AreEqual("pulled", loaded.interactableStates[0].state);
        }

        [Test]
        public void ActiveStatusEffect_RestoresWithSavedRemainingTime()
        {
            var effect = ScriptableObject.CreateInstance<StatusEffect>();
            effect.durationInSeconds = 10f;
            effect.tickInterval = 1f;
            try
            {
                var restored = new ActiveStatusEffect(effect, 2.5f);
                Assert.AreEqual(2.5f, restored.RemainingTime, "Restored effect must resume from the saved time, not the full duration");
            }
            finally
            {
                Object.DestroyImmediate(effect);
            }
        }

        [Test]
        public void ActiveQuest_InitializesOneFlagPerObjective()
        {
            var quest = ScriptableObject.CreateInstance<Quest>();
            quest.objectives.Add("Find the key");
            quest.objectives.Add("Open the door");
            try
            {
                var active = new ActiveQuest(quest);
                Assert.AreEqual(2, active.objectivesCompleted.Count);
                CollectionAssert.AreEqual(new[] { false, false }, active.objectivesCompleted);
            }
            finally
            {
                Object.DestroyImmediate(quest);
            }
        }
    }
}
