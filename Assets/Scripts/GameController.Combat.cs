using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Combat, enemy AI, and status-effect logic for GameController.
// This is a partial of the same class defined in GameController.cs.
public partial class GameController
{
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
        // --- 1. Check for Player Stun ---
        if (isPlayerStunned)
        {
            LogText("You are stunned and cannot act!", TextType.GameResponse);
            isPlayerStunned = false; // The stun wears off after one missed turn.

            // The enemy still gets to take its turn even if the player is stunned.
            var stunnedEnemyTarget = roomEnemiesState[currentLocation].Find(e => e.enemyBlueprint.enemyName.ToLower() == nounPhrase);
            if (stunnedEnemyTarget != null)
            {
                EnemyAttackTurn(stunnedEnemyTarget);
            }
            return;
        }
        // --- 2. Find the Target ---
        var targetEnemy = roomEnemiesState[currentLocation].Find(e => e.enemyBlueprint.enemyName.ToLower() == nounPhrase);
        if (targetEnemy == null)
        {
            var targetCharacter = roomCharactersState[currentLocation].Find(c => c.characterName.ToLower() == nounPhrase);
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
            LogText($"You attack the {nounPhrase}!");
            int totalPlayerBaseAttack = playerStats.baseAttack;
            int playerDamageDealt = Random.Range(totalPlayerBaseAttack - playerDamageVariance, totalPlayerBaseAttack + playerDamageVariance + 1);
            targetEnemy.currentHealth -= playerDamageDealt;
            LogText($"You deal {playerDamageDealt} damage. The {nounPhrase} has {targetEnemy.currentHealth} health remaining.");
        }
        // --- 4. Check for Enemy Defeat ---
        if (targetEnemy.currentHealth <= 0)
        {
            LogText($"You have defeated the {nounPhrase}!");
            onEnemyDefeated.Invoke(targetEnemy);
            GrantXp(targetEnemy.enemyBlueprint.xpReward);
            // Drop loot
            if (targetEnemy.enemyBlueprint.lootDrops.Count > 0)
            {
                foreach (Item itemToDrop in targetEnemy.enemyBlueprint.lootDrops)
                {
                    roomItemsState[currentLocation].Add(itemToDrop);
                    LogText($"The {nounPhrase} dropped a <color={keywordColor}>{itemToDrop.itemName}</color>.", TextType.GameResponse);
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
                    LogText($"Defeating the {nounPhrase} has revealed a new exit to the {direction}!", TextType.GameResponse);
                }
            }
            // Remove the defeated enemy from the room and refresh the view.
            roomEnemiesState[currentLocation].Remove(targetEnemy);
            DisplayLocation(useTypewriter: false);
            return; // End the turn here since the enemy is defeated.
        }
        // --- 5. Enemy's Turn ---
        // If the enemy survived the player's attack, it gets to take its turn.
        // All of its AI logic is now handled in this single function call.
        EnemyAttackTurn(targetEnemy);
        // --- 6. Process Status Effects ---
        // After the full combat exchange, process any status effects on the player.
        ProcessStatusEffects();
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
            target = roomEnemiesState[currentLocation].Find(e => e.enemyBlueprint.enemyName.ToLower() == targetName);
            if (target == null) { LogText("There is no " + targetName + " here."); return; }
        }

        // --- Execution ---
        playerStats.currentMana -= skillToCast.manaCost;
        LogText($"You cast {skillToCast.skillName}!", TextType.GameResponse);
        ProcessActiveSkillEffects(skillToCast, target);

        // Give the enemy a turn if a targeted skill was used
        if (target != null)
        {
            EnemyAttackTurn(target);
        }
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
                // This applies the linked status effect to the player
                if (ability.effectToApplyOnSuccess != null && Random.value <= ability.effectToApplyOnSuccess.chanceToApply)
                {
                    ActiveStatusEffect newEffect = new ActiveStatusEffect(ability.effectToApplyOnSuccess);
                    activeStatusEffects.Add(newEffect);
                    LogText(newEffect.effect.applicationMessage);
                    RecalculatePlayerStats(); // Recalculate stats immediately
                }
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
        int armorDefense = (equippedArmor != null ? equippedArmor.defenseBonus : 0);
        // We need to get the defense bonus from our RecalculatePlayerStats function.
        // Let's assume you've already calculated it there.
        // For clarity, let's just recalculate the specific part we need here.
        int defense_bonus_from_effects = 0;
        foreach (ActiveStatusEffect statusEffect in activeStatusEffects)
        {
            if (statusEffect.effect.effectType == EffectType.IncreaseDefense)
                defense_bonus_from_effects += statusEffect.effect.magnitude;
            else if (statusEffect.effect.effectType == EffectType.DecreaseDefense)
                defense_bonus_from_effects -= statusEffect.effect.magnitude;
        }
        int finalPlayerDefense = armorDefense + defense_bonus_from_effects;
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

    private void ProcessStatusEffects()
    {
        // Loop backwards so we can safely remove expired effects
        for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
        {
            // Tick returns true when the effect has expired
            if (activeStatusEffects[i].Tick(Time.deltaTime, this))
            {
                LogText($"{activeStatusEffects[i].effect.effectName} has worn off.",
                        TextType.GameResponse);
                activeStatusEffects.RemoveAt(i);
            }
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
