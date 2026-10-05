# Riding perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## RidingFullSpeed

Choice leaf: `perk.RidingFullSpeed.choice`. Required skill: 25; alternative: `RidingNimbleStead`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2074`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingFullSpeed.primary | {VALUE}% charge damage dealt. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingFullSpeed.secondary | {VALUE}% charge damage dealt by troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.Mounted |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 349 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 350 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingNimbleStead

Choice leaf: `perk.RidingNimbleStead.choice`. Required skill: 25; alternative: `RidingFullSpeed`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2075`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingNimbleStead.primary | {VALUE}% maneuvering. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingNimbleStead.secondary | {VALUE} riding skill to troops in your formation. | Captain | 30f | Add | TroopUsageFlags.Mounted |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 288 | 320 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.UpdateHorseStats](../source-paths/SandboxAgentStatCalculateModel.md) | 1198 | 1236 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingWellStraped

Choice leaf: `perk.RidingWellStraped.choice`. Required skill: 50; alternative: `RidingVeterinary`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2076`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingWellStraped.primary | {VALUE}% chance of your mount dying or becoming lame after it falls in battle. | Personal | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingWellStraped.secondary | {VALUE} daily loyalty to the governed settlement. | Governor | 0.5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToGovernorPerks](../source-paths/DefaultSettlementLoyaltyModel.md) | 131 | 167 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingVeterinary

Choice leaf: `perk.RidingVeterinary.choice`. Required skill: 50; alternative: `RidingWellStraped`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2077`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingVeterinary.primary | {VALUE}% hit points to your mount. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingVeterinary.secondary | {VALUE}% hit points to mounts of troops in your party. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveMaxHealth](../source-paths/SandboxAgentStatCalculateModel.md) | 777 | 782 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveMaxHealth](../source-paths/SandboxAgentStatCalculateModel.md) | 778 | 782 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingNomadicTraditions

Choice leaf: `perk.RidingNomadicTraditions.choice`. Required skill: 75; alternative: `RidingDeeperSacks`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2078`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingNomadicTraditions.primary | {VALUE}% party speed bonus from footmen on horses. | PartyLeader | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingNomadicTraditions.secondary | {VALUE}% melee damage bonus from speed to mounted troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.Mounted &#124; TroopUsageFlags.Melee |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateLandBaseSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 144 | 223 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateLandBaseSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 146 | 223 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingDeeperSacks

Choice leaf: `perk.RidingDeeperSacks.choice`. Required skill: 75; alternative: `RidingNomadicTraditions`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2079`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingDeeperSacks.primary | {VALUE}% carrying capacity for pack animals in your party. | PartyLeader | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingDeeperSacks.secondary | {VALUE}% trade penalty for mounts. | PartyLeader | -0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 126 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 128 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultInventoryCapacityModel.CalculateInventoryCapacity](../source-paths/DefaultInventoryCapacityModel.md) | 84 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultInventoryCapacityModel.CalculateInventoryCapacity](../source-paths/DefaultInventoryCapacityModel.md) | 86 | 100 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingSagittarius

Choice leaf: `perk.RidingSagittarius.choice`. Required skill: 100; alternative: `RidingSweepingWind`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2080`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingSagittarius.primary | {VALUE}% accuracy penalty while mounted. | Personal | -0.15f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingSagittarius.secondary | {VALUE}% accuracy penalty to mounted troops in your formation. | Captain | -0.15f | AddFactor | TroopUsageFlags.Mounted &#124; TroopUsageFlags.Ranged |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1387 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1389 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1390 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1392 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1394 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1395 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingSweepingWind

Choice leaf: `perk.RidingSweepingWind.choice`. Required skill: 100; alternative: `RidingSagittarius`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2081`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingSweepingWind.primary | {VALUE}% top speed to your mount. | Personal | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingSweepingWind.secondary | {VALUE}% party speed. | PartyLeader | 0.02f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateLandBaseSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 159 | 223 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateLandBaseSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 161 | 223 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.UpdateHorseStats](../source-paths/SandboxAgentStatCalculateModel.md) | 1199 | 1236 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingReliefForce

Choice leaf: `perk.RidingReliefForce.choice`. Required skill: 125; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2082`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingReliefForce.primary | {VALUE} starting battle morale when you join an ongoing battle of your allies. | PartyLeader | 10f | Add | TroopUsageFlags.Undefined |
| perk.RidingReliefForce.secondary | {VALUE}% security provided by mounted troops in the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculateGarrisonEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 213 | 232 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculateGarrisonEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 216 | 232 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingMountedWarrior

Choice leaf: `perk.RidingMountedWarrior.choice`. Required skill: 150; alternative: `RidingHorseArcher`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2083`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingMountedWarrior.primary | {VALUE}% mounted melee damage. | Personal | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingMountedWarrior.secondary | {VALUE}% mounted melee damage by troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.Mounted &#124; TroopUsageFlags.Melee |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 210 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 211 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingHorseArcher

Choice leaf: `perk.RidingHorseArcher.choice`. Required skill: 150; alternative: `RidingMountedWarrior`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2084`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingHorseArcher.primary | {VALUE}% ranged damage while mounted. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingHorseArcher.secondary | {VALUE}% damage by mounted archers in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.Mounted &#124; TroopUsageFlags.BowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 337 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 338 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingShepherd

Choice leaf: `perk.RidingShepherd.choice`. Required skill: 175; alternative: `RidingBreeder`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2085`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingShepherd.primary | {VALUE}% herding speed penalty. | PartyLeader | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingShepherd.secondary | {VALUE}% chance of producing tier 2 horses in villages bound to the governed settlement. | Governor | 0.15f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateLandBaseSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 180 | 223 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateLandBaseSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 182 | 223 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVillageProductionCalculatorModel.CalculateDailyProductionAmount](../source-paths/DefaultVillageProductionCalculatorModel.md) | 32 | 74 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingBreeder

Choice leaf: `perk.RidingBreeder.choice`. Required skill: 175; alternative: `RidingShepherd`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2086`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingBreeder.primary | {VALUE}% daily chance of animals in your party reproducing. | PartyLeader | 0.01f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingBreeder.secondary | {VALUE}% production rate to villages bound to the governed settlement. | Governor | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVillageProductionCalculatorModel.CalculateDailyProductionAmount](../source-paths/DefaultVillageProductionCalculatorModel.md) | 53 | 74 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingThunderousCharge

Choice leaf: `perk.RidingThunderousCharge.choice`. Required skill: 200; alternative: `RidingAnnoyingBuzz`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2087`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingThunderousCharge.primary | {VALUE}% battle morale penalty to enemies with mounted melee kills. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingThunderousCharge.secondary | {VALUE}% battle morale penalty to enemies with mounted melee kills by troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.Mounted &#124; TroopUsageFlags.Melee |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingAnnoyingBuzz

Choice leaf: `perk.RidingAnnoyingBuzz.choice`. Required skill: 200; alternative: `RidingThunderousCharge`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2088`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingAnnoyingBuzz.primary | {VALUE}% battle morale penalty to enemies with mounted ranged kills. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingAnnoyingBuzz.secondary | {VALUE}% battle morale penalty to enemies with mounted ranged kills by troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.Mounted &#124; TroopUsageFlags.Ranged |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingMountedPatrols

Choice leaf: `perk.RidingMountedPatrols.choice`. Required skill: 225; alternative: `RidingCavalryTactics`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2089`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingMountedPatrols.primary | {VALUE}% escape chance to prisoners in your party. | PartyLeader | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingMountedPatrols.secondary | {VALUE}% escape chance to prisoners in the governed settlement. | Governor | -0.5f | AddFactor | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingCavalryTactics

Choice leaf: `perk.RidingCavalryTactics.choice`. Required skill: 225; alternative: `RidingMountedPatrols`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2090`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingCavalryTactics.primary | {VALUE}% volunteering rate of cavalry troops in the settlements governed by your clan. | ClanLeader | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingCavalryTactics.secondary | {VALUE}% wages of mounted troops in the governed settlement. | Governor | -0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 136 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.GetDailyVolunteerProductionProbability](../source-paths/DefaultVolunteerModel.md) | 104 | 109 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.GetDailyVolunteerProductionProbability](../source-paths/DefaultVolunteerModel.md) | 106 | 109 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingDauntlessSteed

Choice leaf: `perk.RidingDauntlessSteed.choice`. Required skill: 250; alternative: `RidingToughSteed`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2091`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingDauntlessSteed.primary | {VALUE}% resistance to getting staggered while mounted. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingDauntlessSteed.secondary | {VALUE} armor to all equipped armor pieces of mounted troops in your formation. | Captain | 5f | Add | TroopUsageFlags.Mounted |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.CalculateStaggerThresholdDamage](../source-paths/SandboxAgentApplyDamageModel.md) | 1005 | 1029 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1531 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1533 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingToughSteed

Choice leaf: `perk.RidingToughSteed.choice`. Required skill: 250; alternative: `RidingDauntlessSteed`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2092`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingToughSteed.primary | {VALUE}% armor to your mount. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.RidingToughSteed.secondary | {VALUE} armor to mounts of troops in your formation. | Captain | 10f | Add | TroopUsageFlags.Mounted |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.UpdateHorseStats](../source-paths/SandboxAgentStatCalculateModel.md) | 1202 | 1236 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.UpdateHorseStats](../source-paths/SandboxAgentStatCalculateModel.md) | 1203 | 1236 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## RidingTheWayOfTheSaddle

Choice leaf: `perk.RidingTheWayOfTheSaddle.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2093`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.RidingTheWayOfTheSaddle.primary | {VALUE} charge damage and maneuvering for every skill point above 250. | Personal | 0.3f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 351 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 353 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.UpdateHorseStats](../source-paths/SandboxAgentStatCalculateModel.md) | 1204 | 1236 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.UpdateHorseStats](../source-paths/SandboxAgentStatCalculateModel.md) | 1206 | 1236 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
