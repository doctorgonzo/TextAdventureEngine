namespace TextEngine
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    // Combat, enemy AI, and status-effect logic for GameController.
    // This is a partial of the same class defined in GameController.cs.
    public partial class GameController
    {
        // Sums the temporary attack modifier from all active status effects.
        // Applied fresh at attack time so buffs/debuffs take effect (and expire)
        // without needing a full stat recalculation.
        private int GetAttackBonusFromEffects()
        {
            if (!engineSettings.useStatusEffectSystem) return 0;
            int bonus = 0;
            foreach (ActiveStatusEffect statusEffect in activeStatusEffects)
            {
                if (statusEffect.effect.effectType == EffectType.IncreaseAttack)
                    bonus += statusEffect.effect.magnitude;
                else if (statusEffect.effect.effectType == EffectType.DecreaseAttack)
                    bonus -= statusEffect.effect.magnitude;
            }
            return bonus;
        }

        // Sums the temporary defense modifier from all active status effects.
        private int GetDefenseBonusFromEffects()
        {
            if (!engineSettings.useStatusEffectSystem) return 0;
            int bonus = 0;
            foreach (ActiveStatusEffect statusEffect in activeStatusEffects)
            {
                if (statusEffect.effect.effectType == EffectType.IncreaseDefense)
                    bonus += statusEffect.effect.magnitude;
                else if (statusEffect.effect.effectType == EffectType.DecreaseDefense)
                    bonus -= statusEffect.effect.magnitude;
            }
            return bonus;
        }

        public void ApplyTimedEffect(StatusEffect effect)
        {
            if (!engineSettings.useStatusEffectSystem) return;
            switch (effect.effectType)
            {
                case EffectType.DamageOverTime:
                    playerCurrentHealth = Mathf.Max(0, playerCurrentHealth - effect.magnitude);
                    LogText(effect.effectMessage, TextType.GameResponse);
                    if (playerCurrentHealth <= 0)
                    {
                        LogText("You have succumbed to your wounds...", TextType.Narrative);
                        currentGameState = GameState.GameOver;
                    }
                    break;

                case EffectType.HealOverTime:
                    playerCurrentHealth = Mathf.Min(playerStats.maxHealth, playerCurrentHealth + effect.magnitude);
                    LogText(effect.effectMessage, TextType.GameResponse);
                    break;

                    // …handle other EffectTypes if you add them…
            }
        }

        void HandleAttack(string nounPhrase)
        {
            bool ambiguous;
            // --- 1. Check for Player Stun ---
            if (isPlayerStunned)
            {
                LogText("You are stunned and cannot act!", TextType.GameResponse);
                isPlayerStunned = false; // The stun wears off after one missed turn.

                // The enemy still gets to take its turn even if the player is stunned.
                var stunnedEnemyTarget = FindByNoun(roomEnemiesState[currentLocation], e => e.enemyBlueprint.enemyName, nounPhrase, out ambiguous);
                if (stunnedEnemyTarget != null)
                {
                    EnemyAttackTurn(stunnedEnemyTarget);
                }
                return;
            }
            // --- 2. Find the Target ---
            var targetEnemy = FindByNoun(roomEnemiesState[currentLocation], e => e.enemyBlueprint.enemyName, nounPhrase, out ambiguous);
            if (ambiguous) return;
            if (targetEnemy == null)
            {
                var targetCharacter = FindByNoun(roomCharactersState[currentLocation], c => c.characterName, nounPhrase, out ambiguous);
                if (ambiguous) return;
                if (targetCharacter != null)
                {
                    LogText($"Attacking the {targetCharacter.characterName} seems like a terrible idea.");
                }
                else
                {
                    LogText("There is no " + nounPhrase + " here to attack.");
                }
                return;
            }
            // --- 3. Player's Attack Phase ---
            // Calculate the final chance to hit by subtracting the enemy's evasion from the player's hit chance.
            float finalHitChance = playerStats.hitChance - targetEnemy.enemyBlueprint.evasionChance;
            // Perform the "to-hit roll".
            if (Random.value > finalHitChance)
            {
                LogText($"You swing at the {targetEnemy.enemyBlueprint.enemyName} but miss!");
            }
            else
            {
                // If the attack hits, calculate and apply damage.
                string enemyName = targetEnemy.enemyBlueprint.enemyName;
                LogText($"You attack the {enemyName}!");
                int totalPlayerBaseAttack = Mathf.Max(0, playerStats.baseAttack + GetAttackBonusFromEffects());
                int playerDamageDealt = Random.Range(totalPlayerBaseAttack - playerDamageVariance, totalPlayerBaseAttack + playerDamageVariance + 1);
                targetEnemy.currentHealth -= playerDamageDealt;
                LogText($"You deal {playerDamageDealt} damage. The {enemyName} has {targetEnemy.currentHealth} health remaining.");
            }
            // --- 4. Check for Enemy Defeat ---
            if (targetEnemy.currentHealth <= 0)
            {
                ResolveEnemyDefeat(targetEnemy);
                return; // End the turn here since the enemy is defeated.
            }
            // --- 5. Enemy's Turn ---
            // If the enemy survived the player's attack, it gets to take its turn.
            // All of its AI logic is now handled in this single function call.
            // (Status effects tick in real time from Update(), so no extra
            // per-exchange processing is needed here.)
            EnemyAttackTurn(targetEnemy);
        }

        private void HandleCast(string nounPhrase)
        {
            string skillName;
            string targetName = null;

            // Parse for "on" to separate skill name from target name
            if (nounPhrase.Contains(" on "))
            {
                var parts = nounPhrase.Split(new[] { " on " }, 2, System.StringSplitOptions.None);
                skillName = parts[0];
                targetName = parts[1];
            }
            else
            {
                skillName = nounPhrase;
            }

            var skillToCast = learnedSkills.FirstOrDefault(s => s.skillName.ToLower() == skillName.ToLower());

            // --- Validation ---
            if (skillToCast == null) { LogText("You don't know that skill."); return; }
            if (skillToCast.skillType != SkillType.Active) { LogText("That is a passive skill and cannot be cast."); return; }
            if (playerStats.currentMana < skillToCast.manaCost) { LogText("You don't have enough mana."); return; }

            EnemyInstance target = null;
            if (skillToCast.targetType == SkillTargetType.Enemy)
            {
                if (string.IsNullOrEmpty(targetName)) { LogText("That skill requires a target. (cast " + skillName + " on [target])"); return; }
                target = FindByNoun(roomEnemiesState[currentLocation], e => e.enemyBlueprint.enemyName, targetName, out bool ambiguous);
                if (ambiguous) return;
                if (target == null) { LogText("There is no " + targetName + " here."); return; }
            }

            // Being stunned blocks spellcasting just like melee attacks. The stun
            // is consumed, and a targeted enemy still gets its free turn.
            if (isPlayerStunned)
            {
                LogText("You are stunned and cannot act!", TextType.GameResponse);
                isPlayerStunned = false;
                if (target != null) EnemyAttackTurn(target);
                return;
            }

            // --- Execution ---
            playerStats.currentMana -= skillToCast.manaCost;
            LogText($"You cast {skillToCast.skillName}!", TextType.GameResponse);
            ProcessActiveSkillEffects(skillToCast, target);

            // Give the enemy a turn if a targeted skill was used — but only if the
            // spell didn't already kill it. A lethal cast resolves the defeat
            // (loot/XP) and the corpse does not retaliate.
            if (target != null)
            {
                if (target.currentHealth <= 0)
                {
                    ResolveEnemyDefeat(target);
                    return;
                }
                EnemyAttackTurn(target);
            }
        }

        // Runs the shared "enemy died" sequence: rewards, loot, exit reveal, and
        // removal. Called from both melee (HandleAttack) and spells (HandleCast) so
        // a killing blow resolves the same way regardless of how it was dealt.
        private void ResolveEnemyDefeat(EnemyInstance targetEnemy)
        {
            string enemyName = targetEnemy.enemyBlueprint.enemyName;
            LogText($"You have defeated the {enemyName}!");
            onEnemyDefeated.Invoke(targetEnemy);
            GrantXp(targetEnemy.enemyBlueprint.xpReward);
            // Drop loot
            if (targetEnemy.enemyBlueprint.lootDrops.Count > 0)
            {
                foreach (Item itemToDrop in targetEnemy.enemyBlueprint.lootDrops)
                {
                    if (itemToDrop == null) continue;
                    roomItemsState[currentLocation].Add(new ItemInstance(itemToDrop));
                    LogText($"The {enemyName} dropped a <color={keywordColor}>{itemToDrop.itemName}</color>.", TextType.GameResponse);
                }
            }
            // Reveal exit on death
            if (!string.IsNullOrEmpty(targetEnemy.enemyBlueprint.exitDirectionToReveal))
            {
                string direction = targetEnemy.enemyBlueprint.exitDirectionToReveal.ToLower();
                Exit exitToReveal = currentLocation.exits.FirstOrDefault(e => e.direction.ToLower() == direction);
                if (exitToReveal != null)
                {
                    exitVisibilityState[exitToReveal] = true;
                    LogText($"Defeating the {enemyName} has revealed a new exit to the {direction}!", TextType.GameResponse);
                }
            }
            // Remove the defeated enemy from the room and refresh the view.
            roomEnemiesState[currentLocation].Remove(targetEnemy);
            DisplayLocation(useTypewriter: false);
        }

        private void ProcessActiveSkillEffects(Skill skill, EnemyInstance target)
        {
            foreach (var effect in skill.activeEffects)
            {
                switch (effect.effectType)
                {
                    case ActiveSkillEffectType.DealDamage:
                        if (target != null)
                        {
                            target.currentHealth -= effect.intParameter;
                            LogText($"The {target.enemyBlueprint.enemyName} takes {effect.intParameter} damage!", TextType.GameResponse);
                            // You could add a check here if the enemy is defeated
                        }
                        break;
                    case ActiveSkillEffectType.HealSelf:
                        playerCurrentHealth = Mathf.Min(playerStats.maxHealth, playerCurrentHealth + effect.intParameter);
                        LogText($"You restore {effect.intParameter} health.", TextType.GameResponse);
                        break;
                    case ActiveSkillEffectType.ApplyStatusEffectToTarget:
                        if (target != null && effect.statusEffectParameter != null)
                        {
                            // This logic can be simplified by creating a helper function
                            // to apply status effects to a target (player or enemy).
                            // For now, we'll just log it.
                            LogText($"You apply {effect.statusEffectParameter.effectName} to the {target.enemyBlueprint.enemyName}.", TextType.GameResponse);
                        }
                        break;
                    case ActiveSkillEffectType.ApplyStatusEffectToSelf:
                        if (effect.statusEffectParameter != null)
                        {
                            ActiveStatusEffect newEffect = new ActiveStatusEffect(effect.statusEffectParameter);
                            activeStatusEffects.Add(newEffect);
                            LogText(newEffect.effect.applicationMessage);
                        }
                        break;
                }
            }
        }

        void EnemyAttackTurn(EnemyInstance attacker)
        {
            // First, check if the player dodges the entire turn.
            if (Random.value < playerStats.dodgeChance)
            {
                LogText($"You nimbly dodge the {attacker.enemyBlueprint.enemyName}'s attack!", TextType.GameResponse);
                return;
            }
            // If there's no behavior asset, perform a simple attack.
            if (attacker.enemyBlueprint.behavior == null)
            {
                PerformStandardAttack(attacker);
                return;
            }
            // --- AI Decision Making ---
            // Loop through the prioritized actions in the behavior asset.
            foreach (AIAction aiAction in attacker.enemyBlueprint.behavior.prioritizedActions)
            {
                bool allConditionsMet = true;
                // Check every condition for this action.
                foreach (AICondition condition in aiAction.conditions)
                {
                    if (!CheckAICondition(condition, attacker))
                    {
                        allConditionsMet = false;
                        break; // If one condition fails, move to the next action.
                    }
                }
                // If all conditions for this action were met, execute it and end the turn.
                if (allConditionsMet)
                {
                    ExecuteSpecialAbility(aiAction.abilityToUse, attacker);
                    return; // The enemy has taken its action for the turn.
                }
            }
            // If no special actions had their conditions met, perform a standard attack.
            PerformStandardAttack(attacker);
        }

        // This is a new helper function to check a single AI condition.
        private bool CheckAICondition(AICondition condition, EnemyInstance self)
        {
            switch (condition.conditionType)
            {
                case AIConditionType.MyHealthPercent_LessThan:
                    float myHealthPercent = (float)self.currentHealth / self.enemyBlueprint.maxHealth;
                    return myHealthPercent < condition.floatParameter;
                case AIConditionType.MyHealthPercent_GreaterThan:
                    myHealthPercent = (float)self.currentHealth / self.enemyBlueprint.maxHealth;
                    return myHealthPercent > condition.floatParameter;
                case AIConditionType.PlayerHealthPercent_LessThan:
                    float playerHealthPercent = (float)playerCurrentHealth / playerStats.maxHealth;
                    return playerHealthPercent < condition.floatParameter;

                    // You can add more cases here for other conditions in the future.
            }
            return false;
        }

        // This is a new helper function to execute a chosen special ability.
        private void ExecuteSpecialAbility(SpecialAbility ability, EnemyInstance attacker)
        {
            LogText(ability.successDescription);
            // Handle the primary effect of the ability (Stun, Heal, etc.)
            // This logic can be expanded from your old HandleAttack function.
            switch (ability.effect)
            {
                case AbilityEffect.SelfHeal:
                    attacker.currentHealth += ability.magnitude;
                    attacker.currentHealth = Mathf.Min(attacker.currentHealth, attacker.enemyBlueprint.maxHealth);
                    LogText($"{attacker.enemyBlueprint.enemyName} heals for {ability.magnitude} health!", TextType.GameResponse);
                    break;
                case AbilityEffect.StunPlayer:
                    isPlayerStunned = true;
                    break;
                case AbilityEffect.DrainMana:
                    playerStats.currentMana = Mathf.Max(0, playerStats.currentMana - ability.magnitude);
                    LogText($"Your mind feels hazy. The {attacker.enemyBlueprint.enemyName} drains {ability.magnitude} mana!", TextType.GameResponse);
                    break;
                case AbilityEffect.CleanseDebuffs:
                    // This is a placeholder for logic that would remove negative effects from the enemy.
                    LogText($"The {attacker.enemyBlueprint.enemyName} purges its negative effects!", TextType.GameResponse);
                    break;
                case AbilityEffect.ApplyDebuffToPlayer:
                    // The linked status effect is applied by the shared
                    // effectToApplyOnSuccess block below (applying it here as well
                    // used to stack the debuff twice per use).
                    break;
                case AbilityEffect.ApplyBuffToSelf:
                    // This applies the linked status effect to the enemy itself.
                    // This requires a similar status effect system for enemies.
                    LogText($"The {attacker.enemyBlueprint.enemyName} empowers itself!", TextType.GameResponse);
                    break;
            }
            // Apply any secondary status effects.
            if (ability.effectToApplyOnSuccess != null && Random.value <= ability.effectToApplyOnSuccess.chanceToApply)
            {
                ActiveStatusEffect newEffect = new ActiveStatusEffect(ability.effectToApplyOnSuccess);
                activeStatusEffects.Add(newEffect);
                LogText(newEffect.effect.applicationMessage);
            }
        }

        // This new function contains the simple logic for a standard attack.
        private void PerformStandardAttack(EnemyInstance attacker)
        {
            LogText($"The {attacker.enemyBlueprint.enemyName} attacks you!", TextType.GameResponse);
            int armorDefense = (equippedArmor != null ? equippedArmor.blueprint.defenseBonus : 0);
            int finalPlayerDefense = armorDefense + GetDefenseBonusFromEffects();
            int enemyDamageDealt = Random.Range(attacker.enemyBlueprint.baseAttack - attacker.enemyBlueprint.damageVariance, attacker.enemyBlueprint.baseAttack + attacker.enemyBlueprint.damageVariance + 1);
            // Make sure damage doesn't go below zero (which would cause healing)
            int damageTaken = Mathf.Max(0, enemyDamageDealt - finalPlayerDefense);
            playerCurrentHealth -= damageTaken;
            LogText($"The {attacker.enemyBlueprint.enemyName} deals {damageTaken} damage. You have {playerCurrentHealth}/{playerStats.maxHealth} health remaining.");
            if (playerCurrentHealth <= 0)
            {
                LogText("You have been defeated... Your vision fades to black.");
                LogText("<color=red>G A M E   O V E R</color>", TextType.Narrative);
                currentGameState = GameState.GameOver;
            }
        }

        private void CheckForAmbushes()
        {
            // Find any enemy in the current room that attacks on sight.
            var ambushers = roomEnemiesState[currentLocation]
                .Where(e => e.enemyBlueprint.attacksOnSight)
                .ToList();
            if (ambushers.Count > 0)
            {
                LogText("<color=red>You've been ambushed!</color>", TextType.Narrative);
                // Each ambusher gets one free attack.
                foreach (var enemy in ambushers)
                {
                    EnemyAttackTurn(enemy);
                    if (currentGameState == GameState.GameOver) return;
                }
            }
        }
    }
}
