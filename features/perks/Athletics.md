# Athletics perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## AthleticsMorningExercise

Choice leaf: `perk.AthleticsMorningExercise.choice`. Required skill: 25; alternative: `AthleticsWellBuilt`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2094`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsMorningExercise.primary | {VALUE}% combat movement speed. | Personal | 0.03f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsMorningExercise.secondary | {VALUE}% combat movement speed to troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1499 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1511 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsWellBuilt

Choice leaf: `perk.AthleticsWellBuilt.choice`. Required skill: 25; alternative: `AthleticsMorningExercise`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2095`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsWellBuilt.primary | {VALUE} hit points. | Personal | 5f | Add | TroopUsageFlags.Undefined |
| perk.AthleticsWellBuilt.secondary | {VALUE} hit points to foot troops in your party. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel.MaxHitpoints](../source-paths/DefaultCharacterStatsModel.md) | 34 | 47 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveMaxHealth](../source-paths/SandboxAgentStatCalculateModel.md) | 739 | 782 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsFury

Choice leaf: `perk.AthleticsFury.choice`. Required skill: 50; alternative: `AthleticsFormFittingArmor`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2096`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsFury.primary | {VALUE}% weapon handling while on foot. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsFury.secondary | {VALUE}% weapon handling to foot troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1318 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1321 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsFormFittingArmor

Choice leaf: `perk.AthleticsFormFittingArmor.choice`. Required skill: 50; alternative: `AthleticsFury`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2097`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsFormFittingArmor.primary | {VALUE}% armor weight. | Personal | -0.15f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsFormFittingArmor.secondary | {VALUE}% combat movement speed to tier 3+ foot troops in your formation. | Captain | 0.04f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveArmorEncumbrance](../source-paths/SandboxAgentStatCalculateModel.md) | 686 | 691 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveArmorEncumbrance](../source-paths/SandboxAgentStatCalculateModel.md) | 688 | 691 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1518 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1574 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsImposingStature

Choice leaf: `perk.AthleticsImposingStature.choice`. Required skill: 75; alternative: `AthleticsStamina`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2098`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsImposingStature.primary | {VALUE}% persuasion chance. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsImposingStature.secondary | {VALUE} party size. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 311 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 313 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPersuasionModel.GetBonusSuccessChance](../source-paths/DefaultPersuasionModel.md) | 81 | 91 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPersuasionModel.GetBonusSuccessChance](../source-paths/DefaultPersuasionModel.md) | 83 | 91 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsStamina

Choice leaf: `perk.AthleticsStamina.choice`. Required skill: 75; alternative: `AthleticsImposingStature`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2099`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsStamina.primary | {VALUE}% crafting stamina recovery rate. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsStamina.secondary | {VALUE} prisoner limit and -10% escape chance to your prisoners. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.AddMobilePartyLeaderPrisonerSizePerkEffects](../source-paths/DefaultPartySizeLimitModel.md) | 217 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.AddMobilePartyLeaderPrisonerSizePerkEffects](../source-paths/DefaultPartySizeLimitModel.md) | 219 | 230 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsSprint

Choice leaf: `perk.AthleticsSprint.choice`. Required skill: 100; alternative: `AthleticsPowerful`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2100`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsSprint.primary | {VALUE}% combat movement speed when you have no shields and no ranged weapons equipped. | Personal | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsSprint.secondary | {VALUE}% combat movement speed to infantry troops in your formation. | Captain | 0.03f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1503 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1522 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsPowerful

Choice leaf: `perk.AthleticsPowerful.choice`. Required skill: 100; alternative: `AthleticsSprint`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2101`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsPowerful.primary | {VALUE}% damage with melee weapons. | Personal | 0.04f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsPowerful.secondary | {VALUE}% melee damage by troops in your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.Melee |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 201 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 202 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsSurgingBlow

Choice leaf: `perk.AthleticsSurgingBlow.choice`. Required skill: 125; alternative: `AthleticsBraced`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2102`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsSurgingBlow.primary | {VALUE}% damage bonus from speed while on foot. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsSurgingBlow.secondary | {VALUE}% damage bonus from speed to troops in your formation. | Captain | 0.3f | AddFactor | TroopUsageFlags.OnFoot |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsBraced

Choice leaf: `perk.AthleticsBraced.choice`. Required skill: 125; alternative: `AthleticsSurgingBlow`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2103`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsBraced.primary | {VALUE}% charge damage taken. | Personal | -0.4f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsBraced.secondary | {VALUE}% charge damage taken by troops in your formation. | Captain | -0.3f | AddFactor | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 580 | 595 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 584 | 595 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsWalkItOff

Choice leaf: `perk.AthleticsWalkItOff.choice`. Required skill: 150; alternative: `AthleticsAGoodDaysRest`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2104`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsWalkItOff.primary | {VALUE}% hit point regeneration while traveling. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsWalkItOff.secondary | {VALUE} daily experience to foot troops while traveling. | PartyLeader | 3f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 171 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingHpForHeroes](../source-paths/DefaultPartyHealingModel.md) | 260 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 52 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 54 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetPerkExperiencesForTroops](../source-paths/DefaultPartyTrainingModel.md) | 104 | 109 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsAGoodDaysRest

Choice leaf: `perk.AthleticsAGoodDaysRest.choice`. Required skill: 150; alternative: `AthleticsWalkItOff`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2105`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsAGoodDaysRest.primary | {VALUE}% hit point regeneration while waiting in settlements. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsAGoodDaysRest.secondary | {VALUE} daily experience to foot troops while waiting in settlements. | PartyLeader | 10f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 190 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingHpForHeroes](../source-paths/DefaultPartyHealingModel.md) | 277 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 60 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 62 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetPerkExperiencesForTroops](../source-paths/DefaultPartyTrainingModel.md) | 104 | 109 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsDurable

Choice leaf: `perk.AthleticsDurable.choice`. Required skill: 175; alternative: `AthleticsEnergetic`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2106`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsDurable.primary | {VALUE} Endurance attribute. | Personal | 1f | Add | TroopUsageFlags.Undefined |
| perk.AthleticsDurable.secondary | {VALUE} daily loyalty in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToGovernorPerks](../source-paths/DefaultSettlementLoyaltyModel.md) | 129 | 167 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsEnergetic

Choice leaf: `perk.AthleticsEnergetic.choice`. Required skill: 175; alternative: `AthleticsDurable`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2107`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsEnergetic.primary | {VALUE}% overburdened speed penalty. | PartyLeader | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsEnergetic.secondary | {VALUE}% hearth growth in villages bound to the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.GetOverburdenedEffect](../source-paths/DefaultPartySpeedCalculatingModel.md) | 250 | 254 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementProsperityModel.CalculateHearthChangeInternal](../source-paths/DefaultSettlementProsperityModel.md) | 58 | 70 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsSteady

Choice leaf: `perk.AthleticsSteady.choice`. Required skill: 200; alternative: `AthleticsStrong`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2108`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsSteady.primary | {VALUE} Control attribute. | Personal | 1f | Add | TroopUsageFlags.Undefined |
| perk.AthleticsSteady.secondary | {VALUE}% production in farms, mines, lumber camps and clay pits bound to the governed settlement. | Governor | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVillageProductionCalculatorModel.CalculateDailyProductionAmount](../source-paths/DefaultVillageProductionCalculatorModel.md) | 47 | 74 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsStrong

Choice leaf: `perk.AthleticsStrong.choice`. Required skill: 200; alternative: `AthleticsSteady`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2109`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsStrong.primary | {VALUE} Vigor attribute. | Personal | 1f | Add | TroopUsageFlags.Undefined |
| perk.AthleticsStrong.secondary | {VALUE}% party speed by foot troops in your party. | PartyLeader | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.GetFootmenPerkBonus](../source-paths/DefaultPartySpeedCalculatingModel.md) | 434 | 439 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.GetFootmenPerkBonus](../source-paths/DefaultPartySpeedCalculatingModel.md) | 436 | 439 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsStrongLegs

Choice leaf: `perk.AthleticsStrongLegs.choice`. Required skill: 225; alternative: `AthleticsStrongArms`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2110`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsStrongLegs.primary | {VALUE}% fall damage taken and +100% kick damage dealt. | Personal | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsStrongLegs.secondary | {VALUE}% food consumption in the governed settlement while under siege. | Governor | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 70 | 87 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 382 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 590 | 595 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsStrongArms

Choice leaf: `perk.AthleticsStrongArms.choice`. Required skill: 225; alternative: `AthleticsStrongLegs`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2111`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsStrongArms.primary | {VALUE}% damage with throwing weapons. | Personal | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsStrongArms.secondary | {VALUE} throwing skill to troops in your formation. | Captain | 20f | Add | TroopUsageFlags.ThrownUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 284 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 272 | 320 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsSpartan

Choice leaf: `perk.AthleticsSpartan.choice`. Required skill: 250; alternative: `AthleticsIgnorePain`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2112`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsSpartan.primary | {VALUE}% resistance to getting staggered while on foot. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsSpartan.secondary | {VALUE}% food consumption in your party. | PartyLeader | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 54 | 87 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.CalculateStaggerThresholdDamage](../source-paths/SandboxAgentApplyDamageModel.md) | 1009 | 1029 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsIgnorePain

Choice leaf: `perk.AthleticsIgnorePain.choice`. Required skill: 250; alternative: `AthleticsSpartan`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2113`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsIgnorePain.primary | {VALUE}% armor while on foot. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsIgnorePain.secondary | {VALUE} armor to all equipped armor pieces of foot troops in your formation. | Captain | 5f | Add | TroopUsageFlags.OnFoot |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1536 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1538 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1547 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1549 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## AthleticsMightyBlow

Choice leaf: `perk.AthleticsMightyBlow.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2114`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.AthleticsMightyBlow.primary | You stun your enemies longer after they block your attack. | Personal | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.AthleticsMightyBlow.secondary | {VALUE} hit points for every skill point above 250. | Personal | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel.MaxHitpoints](../source-paths/DefaultCharacterStatsModel.md) | 41 | 47 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel.MaxHitpoints](../source-paths/DefaultCharacterStatsModel.md) | 44 | 47 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.CalculateDefendedBlowStunMultipliers](../source-paths/SandboxAgentApplyDamageModel.md) | 780 | 788 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.CalculateDefendedBlowStunMultipliers](../source-paths/SandboxAgentApplyDamageModel.md) | 782 | 788 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
