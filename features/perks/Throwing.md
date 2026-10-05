# Throwing perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## ThrowingQuickDraw

Choice leaf: `perk.ThrowingQuickDraw.choice`. Required skill: 25; alternative: `ThrowingShieldBreaker`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2053`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingQuickDraw.primary | {VALUE}% draw speed with throwing weapons. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingQuickDraw.secondary | {VALUE}% draw speed with throwing weapons to troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.ThrownUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1447 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1451 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingShieldBreaker

Choice leaf: `perk.ThrowingShieldBreaker.choice`. Required skill: 25; alternative: `ThrowingQuickDraw`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2054`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingShieldBreaker.primary | {VALUE}% damage to shields with throwing weapons. | Personal | 0.4f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingShieldBreaker.secondary | {VALUE}% damage to shields with throwing weapons by troops in your formation. | Captain | 0.08f | AddFactor | TroopUsageFlags.ThrownUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 287 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 288 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingHunter

Choice leaf: `perk.ThrowingHunter.choice`. Required skill: 50; alternative: `ThrowingFlexibleFighter`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2055`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingHunter.primary | {VALUE}% damage to mounts with throwing weapons. | Personal | 0.4f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingHunter.secondary | {VALUE}% damage to mounts with throwing weapons by troops in your formation. | Captain | 0.08f | AddFactor | TroopUsageFlags.ThrownUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 297 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 298 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingFlexibleFighter

Choice leaf: `perk.ThrowingFlexibleFighter.choice`. Required skill: 50; alternative: `ThrowingHunter`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2056`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingFlexibleFighter.primary | {VALUE}% damage while using throwing weapons as melee. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingFlexibleFighter.secondary | {VALUE} Control skills of infantry, {VALUE} Vigor skills of archers in your formation. | Captain | 15f | Add | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 206 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 254 | 320 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingMountedSkirmisher

Choice leaf: `perk.ThrowingMountedSkirmisher.choice`. Required skill: 75; alternative: `ThrowingWellPrepared`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2057`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingMountedSkirmisher.primary | {VALUE}% accuracy penalty with throwing weapons while mounted. | Personal | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingMountedSkirmisher.secondary | {VALUE}% damage with throwing weapons by mounted troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.Mounted &#124; TroopUsageFlags.ThrownUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 302 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1402 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1404 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1405 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingWellPrepared

Choice leaf: `perk.ThrowingWellPrepared.choice`. Required skill: 75; alternative: `ThrowingMountedSkirmisher`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2058`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingWellPrepared.primary | {VALUE} ammunition for throwing weapons. | Personal | 1f | Add | TroopUsageFlags.Undefined |
| perk.ThrowingWellPrepared.secondary | {VALUE} ammunition for throwing weapons to troops in your party. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeMissionEquipment](../source-paths/SandboxAgentStatCalculateModel.md) | 171 | 202 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeMissionEquipment](../source-paths/SandboxAgentStatCalculateModel.md) | 177 | 202 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingRunningThrow

Choice leaf: `perk.ThrowingRunningThrow.choice`. Required skill: 100; alternative: `ThrowingKnockOff`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2059`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingRunningThrow.primary | {VALUE}% damage bonus from speed with throwing weapons. | Personal | 0.25f | Add | TroopUsageFlags.Undefined |
| perk.ThrowingRunningThrow.secondary | {VALUE} throwing skill to troops in your formation. | Captain | 30f | Add | TroopUsageFlags.ThrownUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 273 | 320 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingKnockOff

Choice leaf: `perk.ThrowingKnockOff.choice`. Required skill: 100; alternative: `ThrowingRunningThrow`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2060`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingKnockOff.primary | Thrown weapons can now dismount and ignore {VALUE}% dismount resistance on attacks against cavalry. | Personal | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingKnockOff.secondary | {VALUE}% throwing weapon damage to cavalry by troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.Mounted &#124; TroopUsageFlags.ThrownUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 307 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.CanWeaponDismount](../source-paths/SandboxAgentApplyDamageModel.md) | 761 | 768 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.GetDismountPenetration](../source-paths/SandboxAgentApplyDamageModel.md) | 885 | 892 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.GetDismountPenetration](../source-paths/SandboxAgentApplyDamageModel.md) | 887 | 892 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingSkirmisher

Choice leaf: `perk.ThrowingSkirmisher.choice`. Required skill: 125; alternative: `ThrowingSaddlebags`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2061`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingSkirmisher.primary | {VALUE}% damage taken by ranged attacks while holding a throwing weapon. | Personal | -0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingSkirmisher.secondary | {VALUE}% damage taken by ranged attacks to troops in your formation. | Captain | -0.03f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 521 | 595 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 535 | 595 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingSaddlebags

Choice leaf: `perk.ThrowingSaddlebags.choice`. Required skill: 125; alternative: `ThrowingSkirmisher`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2062`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingSaddlebags.primary | {VALUE} ammunition for throwing weapons when you start a battle mounted. | Personal | 2f | Add | TroopUsageFlags.Undefined |
| perk.ThrowingSaddlebags.secondary | {VALUE} daily experience to infantry troops in your party. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 56 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 58 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetPerkExperiencesForTroops](../source-paths/DefaultPartyTrainingModel.md) | 104 | 109 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeMissionEquipment](../source-paths/SandboxAgentStatCalculateModel.md) | 175 | 202 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingFocus

Choice leaf: `perk.ThrowingFocus.choice`. Required skill: 150; alternative: `ThrowingLastHit`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2063`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingFocus.primary | {VALUE}% zoom with throwing weapons. | Personal | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingFocus.secondary | {VALUE} daily security in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculatePerkEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 284 | 287 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetMaxCameraZoom](../source-paths/SandboxAgentStatCalculateModel.md) | 558 | 563 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingLastHit

Choice leaf: `perk.ThrowingLastHit.choice`. Required skill: 150; alternative: `ThrowingFocus`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2064`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingLastHit.primary | {VALUE}% damage to enemies with less than half of their hit points left. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingLastHit.secondary | {VALUE} starting battle morale to troops in your party. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 311 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingHeadHunter

Choice leaf: `perk.ThrowingHeadHunter.choice`. Required skill: 175; alternative: `ThrowingSlingingCompetitions`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2065`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingHeadHunter.primary | {VALUE}% headshot damage with thrown weapons. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingHeadHunter.secondary | {VALUE}% recruitment cost of tier 2+ troops. | PartyLeader | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 235 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 237 | 287 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 315 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingSlingingCompetitions

Choice leaf: `perk.ThrowingSlingingCompetitions.choice`. Required skill: 175; alternative: `ThrowingHeadHunter`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2066`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingSlingingCompetitions.primary | Sling weapons can penetrate head armor. | Personal | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingSlingingCompetitions.secondary | {VALUE} militia recruitment in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.GetSettlementMilitiaChangeDueToPerks](../source-paths/DefaultSettlementMilitiaModel.md) | 173 | 180 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingResourceful

Choice leaf: `perk.ThrowingResourceful.choice`. Required skill: 200; alternative: `ThrowingSplinters`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2067`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingResourceful.primary | {VALUE} ammunition for throwing weapons. | Personal | 2f | Add | TroopUsageFlags.Undefined |
| perk.ThrowingResourceful.secondary | {VALUE}% experience from battles to troops in your party equipped with throwing weapons. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 94 | 121 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 96 | 121 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeMissionEquipment](../source-paths/SandboxAgentStatCalculateModel.md) | 172 | 202 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingSplinters

Choice leaf: `perk.ThrowingSplinters.choice`. Required skill: 200; alternative: `ThrowingResourceful`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2068`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingSplinters.primary | Triple damage against shields with throwing axes. | Personal | 3f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingSplinters.secondary | {VALUE}% damage to shields with throwing weapons by troops in your formation. | Captain | 0.5f | AddFactor | TroopUsageFlags.ThrownUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 291 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 293 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingPerfectTechnique

Choice leaf: `perk.ThrowingPerfectTechnique.choice`. Required skill: 225; alternative: `ThrowingLongReach`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2069`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingPerfectTechnique.primary | {VALUE}% travel speed to your throwing weapons. | Personal | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingPerfectTechnique.secondary | {VALUE}% travel speed to throwing weapons of troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.ThrownUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1448 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1452 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingLongReach

Choice leaf: `perk.ThrowingLongReach.choice`. Required skill: 225; alternative: `ThrowingPerfectTechnique`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2070`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingLongReach.primary | You can pick up items from the ground while mounted. | Personal | 0f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingLongReach.secondary | {VALUE}% morale and renown gained from battles won. | PartyLeader | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.CalculateRenownGain](../source-paths/DefaultBattleRewardModel.md) | 53 | 71 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.CalculateRenownGain](../source-paths/DefaultBattleRewardModel.md) | 55 | 71 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.CalculateMoraleGainVictory](../source-paths/DefaultBattleRewardModel.md) | 92 | 102 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.CalculateMoraleGainVictory](../source-paths/DefaultBattleRewardModel.md) | 94 | 102 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetInteractionDistance](../source-paths/SandboxAgentStatCalculateModel.md) | 510 | 516 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingWeakSpot

Choice leaf: `perk.ThrowingWeakSpot.choice`. Required skill: 250; alternative: `ThrowingImpale`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2071`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingWeakSpot.primary | {VALUE}% armor penetration with throwing weapons. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingWeakSpot.secondary | {VALUE}% armor penetration with throwing weapons by troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.ThrownUser |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingImpale

Choice leaf: `perk.ThrowingImpale.choice`. Required skill: 250; alternative: `ThrowingWeakSpot`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2072`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingImpale.primary | Javelins you throw can penetrate shields. | Personal | 0f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingImpale.secondary | {VALUE}% damage with throwing weapons by troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.ThrownUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 304 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.DecideMissileWeaponFlags](../source-paths/SandboxAgentApplyDamageModel.md) | 679 | 684 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ThrowingUnstoppableForce

Choice leaf: `perk.ThrowingUnstoppableForce.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2073`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ThrowingUnstoppableForce.primary | {VALUE}% travel speed to your throwing weapons for every skill point above 200. | Personal | 0.002f | AddFactor | TroopUsageFlags.Undefined |
| perk.ThrowingUnstoppableForce.secondary | {VALUE}% damage with throwing weapons for every skill point above 200. | Personal | 0.005f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 317 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1454 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
