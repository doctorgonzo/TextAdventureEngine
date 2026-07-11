namespace TextEngine
{
    using System.Linq;
    using System.Text;
    using UnityEngine;

    // Conversation flow and dialogue-node action handling for GameController.
    // This is a partial of the same class defined in GameController.cs.
    public partial class GameController
    {
        void HandleTalk(string nounPhrase)
        {
            bool ambiguous;
            // First, try to find a friendly character with that name.
            var characterToTalkTo = FindByNoun(roomCharactersState[currentLocation], c => c.characterName, nounPhrase, out ambiguous);
            if (ambiguous) return;
            if (characterToTalkTo != null)
            {
                if (characterToTalkTo.startingDialogue != null)
                {
                    currentGameState = GameState.Dialogue;
                    currentDialogueNode = characterToTalkTo.startingDialogue;
                    activeConversationCharacter = characterToTalkTo; // NEW: Remember who we're talking to
                    DisplayCurrentDialogueNode();
                }
                else
                {
                    LogText(characterToTalkTo.characterName + " has nothing to say.");
                }
            }
            else
            {
                // If no friendly character was found, check if an enemy with that name exists.
                var targetEnemy = FindByNoun(roomEnemiesState[currentLocation], e => e.enemyBlueprint.enemyName, nounPhrase, out ambiguous);
                if (ambiguous) return;
                if (targetEnemy != null)
                {
                    // If we find an enemy, give a specific, non-conversational response.
                    LogText($"The {targetEnemy.enemyBlueprint.enemyName} just snarls at you. It doesn't seem interested in conversation.");
                    return;
                }
                // If we didn't find a character OR an enemy, then the target truly isn't here.
                LogText("There is no one called '" + nounPhrase + "' here to talk to.");
            }
        }

        void DisplayCurrentDialogueNode()
        {
            foreach (string flag in currentDialogueNode.requiredFlags)
            {
                // If a required flag is missing or false, jump to the failure node.
                if (!worldFlags.ContainsKey(flag) || !worldFlags[flag])
                {
                    if (currentDialogueNode.failureNode != null)
                    {
                        currentDialogueNode = currentDialogueNode.failureNode;
                        // We call the method again to process the new failure node.
                        DisplayCurrentDialogueNode();
                        return;
                    }
                    else
                    {
                        // If there's no failure node, the conversation just ends.
                        EndDialogue();
                        return;
                    }
                }
            }
            // First, check if this node requires an item for the success path to trigger.
            if (currentDialogueNode.requiredItemForSuccess != null)
            {
                // If it does, check if the player actually has that item in their inventory.
                if (!playerInventory.Any(inst => inst.blueprint == currentDialogueNode.requiredItemForSuccess))
                {
                    // Failure! If the player doesn't have the item, redirect to the failure node.
                    if (currentDialogueNode.failureNode != null)
                    {
                        currentDialogueNode = currentDialogueNode.failureNode;
                    }
                    else
                    {
                        // This is a safe fallback if you forget to set a failure node.
                        LogText("You don't have the required item to continue this conversation.");
                        EndDialogue();
                        return; // Stop processing here.
                    }
                }
            }
            // If we passed the check (or if there was no check), queue up all text blocks.
            // 1. Queue the main NPC dialogue. This will clear the screen.
            PrintToScreen(currentDialogueNode.dialogueText);
            // 2. Process the actions of the current node. This will queue any resulting text.
            ProcessNodeActions(currentDialogueNode);
            // 3. Queue the player's response choices.
            if (currentDialogueNode.playerResponses.Length > 0)
            {
                StringBuilder responseText = new StringBuilder();
                for (int i = 0; i < currentDialogueNode.playerResponses.Length; i++)
                {
                    responseText.Append($"\n{i + 1}. {currentDialogueNode.playerResponses[i].responseText}");
                }
                LogText(responseText.ToString(), TextType.Narrative);
            }
            foreach (string flag in currentDialogueNode.flagsToSet)
            {
                worldFlags[flag] = true;
                if (engineSettings.verboseLogging) Debug.Log($"[Text Engine] Flag set: {flag} = true");
            }
            // A node with no player responses is terminal. End the conversation
            // here — otherwise the game stays in Dialogue state with no valid
            // input and soft-locks. Skip this if a node action already moved us
            // to another state (shop, trainer, minigame, hostility).
            if ((currentDialogueNode.playerResponses == null || currentDialogueNode.playerResponses.Length == 0)
                && currentGameState == GameState.Dialogue)
            {
                EndDialogue();
            }
        }

        private string ProcessNodeActions(DialogueNode node)
        {
            if (node.successActions == null || node.successActions.Count == 0)
            {
                return null; // No actions to process.
            }
            string followUpText = null;
            // Loop through every action on this node and execute it.
            foreach (DialogueAction action in node.successActions)
            {
                switch (action.actionType)
                {
                    case DialogueActionType.GiveItem:
                        TryGiveItem(action.item);
                        break;
                    case DialogueActionType.TakeItem:
                        var heldInstance = action.item != null
                            ? playerInventory.Find(inst => inst.blueprint == action.item)
                            : null;
                        if (heldInstance != null)
                        {
                            playerInventory.Remove(heldInstance);
                            LogText($"You give the <color={keywordColor}>{action.item.itemName}</color>.", TextType.GameResponse);
                        }
                        break;
                    case DialogueActionType.RevealExit:
                        // Find the exit in the current room by its direction.
                        Exit exitToReveal = currentLocation.exits.FirstOrDefault(e => e.direction.ToLower() == action.exitDirection.ToLower());
                        if (exitToReveal != null)
                        {
                            exitVisibilityState[exitToReveal] = true;
                            LogText($"A new exit has opened to the {action.exitDirection}.", TextType.GameResponse);
                            DisplayLocation(useTypewriter: false);
                        }
                        break;
                    case DialogueActionType.StartMiniGame:
                        StartConnectFour();
                        break;
                    case DialogueActionType.BecomeHostile:
                        if (activeConversationCharacter != null && activeConversationCharacter.becomesEnemy != null)
                        {
                            ConvertCharacterToEnemy(activeConversationCharacter);
                            EndDialogue();
                        }
                        break;
                    case DialogueActionType.StartQuest:
                        // Check if the quest isn't already active or completed
                        if (action.quest != null &&
                            !activeQuests.Any(q => q.quest == action.quest) &&
                            !completedQuests.Contains(action.quest))
                        {
                            activeQuests.Add(new ActiveQuest(action.quest));
                            LogText($"New quest started: {action.quest.questName}", TextType.GameResponse);
                        }
                        break;
                    case DialogueActionType.UpdateQuest:
                        // Find the active quest
                        ActiveQuest questToUpdate = activeQuests.FirstOrDefault(q => q.quest == action.quest);
                        if (questToUpdate != null)
                        {
                            // Mark the specified objective as complete. Guard the
                            // index — a bad value in the DialogueAction asset must
                            // not crash the conversation.
                            if (action.objectiveIndex >= 0 && action.objectiveIndex < questToUpdate.objectivesCompleted.Count)
                            {
                                questToUpdate.objectivesCompleted[action.objectiveIndex] = true;
                                LogText($"Quest updated: {action.quest.questName}", TextType.GameResponse);
                            }
                            else
                            {
                                Debug.LogWarning($"UpdateQuest: objective index {action.objectiveIndex} is out of range for quest '{action.quest.questName}' ({questToUpdate.objectivesCompleted.Count} objectives).");
                            }
                        }
                        break;
                    case DialogueActionType.CompleteQuest:
                        // Find the active quest to complete
                        ActiveQuest questToComplete = activeQuests.FirstOrDefault(q => q.quest == action.quest);
                        if (questToComplete != null)
                        {
                            activeQuests.Remove(questToComplete);
                            completedQuests.Add(questToComplete.quest);
                            LogText($"Quest completed: {questToComplete.quest.questName}", TextType.GameResponse);
                            if (questToComplete.quest.currencyReward > 0)
                            {
                                playerStats.currency += questToComplete.quest.currencyReward;
                                LogText($"You receive {questToComplete.quest.currencyReward} coins.", TextType.GameResponse);
                            }

                            // Check for and grant item rewards
                            foreach (Item itemReward in questToComplete.quest.itemRewards)
                            {
                                TryGiveItem(itemReward);
                            }
                            GrantXp(questToComplete.quest.xpReward);
                        }
                        break;
                    case DialogueActionType.OpenSkillShop:
                        // The activeConversationCharacter is the trainer
                        EnterSkillTrainingMode(activeConversationCharacter);
                        break;
                    case DialogueActionType.OpenShop:
                        // The current location is the shop
                        EnterShopMode(currentLocation);
                        break;
                }
            }
            return followUpText;
        }

        void HandleDialogueResponse(string inputText)
        {
            // Try to convert the player's input to a number
            if (int.TryParse(inputText, out int choiceIndex))
            {
                choiceIndex -= 1; // Adjust for zero-based array index
                // Check if the choice is valid
                if (choiceIndex >= 0 && choiceIndex < currentDialogueNode.playerResponses.Length)
                {
                    Response selectedResponse = currentDialogueNode.playerResponses[choiceIndex];
                    // Echo the player's choice
                    LogText("> " + selectedResponse.responseText, TextType.PlayerInput);
                    // Check if this choice leads to another node
                    if (selectedResponse.nextNode != null)
                    {
                        currentDialogueNode = selectedResponse.nextNode;
                        DisplayCurrentDialogueNode();
                    }
                    else // If nextNode is null, the conversation ends
                    {
                        EndDialogue();
                    }
                }
                else
                {
                    LogText("That's not a valid choice. Please type a number from the list.");
                }
            }
            else
            {
                LogText("Please type the number of your choice.");
            }
        }

        void EndDialogue()
        {
            LogText("The conversation has ended.");
            currentGameState = GameState.Playing;
            activeConversationCharacter = null; // NEW: Clear the character when conversation ends
            DisplayLocation(useTypewriter: false); // Refresh instantly
        }
    }
}
