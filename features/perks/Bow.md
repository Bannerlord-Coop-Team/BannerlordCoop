# Bow perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## BowBowControl

Choice leaf: `perk.BowBowControl.choice`. Required skill: 25; alternative: `BowDeadAim`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2011`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowBowControl.primary | {VALUE}% accuracy penalty while moving. | Personal | -0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowBowControl.secondary | {VALUE}% damage with bows by troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.BowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 239 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1412 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowDeadAim

Choice leaf: `perk.BowDeadAim.choice`. Required skill: 25; alternative: `BowBowControl`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2012`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowDeadAim.primary | {VALUE}% headshot damage with bows. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowDeadAim.secondary | {VALUE} Bow skill to troops in your formation. | Captain | 20f | Add | TroopUsageFlags.BowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 242 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 261 | 320 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowBodkin

Choice leaf: `perk.BowBodkin.choice`. Required skill: 50; alternative: `BowNockingPoint`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2013`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowBodkin.primary | {VALUE}% armor penetration with bows. | Personal | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowBodkin.secondary | {VALUE}% armor penetration with bows by troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.BowUser |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowNockingPoint

Choice leaf: `perk.BowNockingPoint.choice`. Required skill: 50; alternative: `BowBodkin`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2014`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowNockingPoint.primary | {VALUE}% movement speed penalty while reloading. | Personal | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowNockingPoint.secondary | {VALUE}% movement speed to archers in your formation. | Captain | 0.03f | AddFactor | TroopUsageFlags.BowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1376 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1423 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowRapidFire

Choice leaf: `perk.BowRapidFire.choice`. Required skill: 75; alternative: `BowQuickAdjustments`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2015`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowRapidFire.primary | {VALUE}% reload speed with bows. | Personal | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowRapidFire.secondary | {VALUE}% reload speed to troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.Ranged |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1413 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1420 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowQuickAdjustments

Choice leaf: `perk.BowQuickAdjustments.choice`. Required skill: 75; alternative: `BowRapidFire`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2016`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowQuickAdjustments.primary | {VALUE}% accuracy penalty while rotating. | Personal | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowQuickAdjustments.secondary | {VALUE}% accuracy penalty to archers in your formation. | Captain | -0.05f | AddFactor | TroopUsageFlags.BowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetWeaponInaccuracy](../source-paths/SandboxAgentStatCalculateModel.md) | 477 | 502 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1414 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowMerryMen

Choice leaf: `perk.BowMerryMen.choice`. Required skill: 100; alternative: `BowMountedArchery`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2017`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowMerryMen.primary | {VALUE} party size. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |
| perk.BowMerryMen.secondary | {VALUE} militia recruitment in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 315 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 317 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.GetSettlementMilitiaChangeDueToPerks](../source-paths/DefaultSettlementMilitiaModel.md) | 171 | 180 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowMountedArchery

Choice leaf: `perk.BowMountedArchery.choice`. Required skill: 100; alternative: `BowMerryMen`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2018`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowMountedArchery.primary | {VALUE}% accuracy penalty using bows while mounted. | Personal | -0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowMountedArchery.secondary | {VALUE}% security provided by archers in the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculateGarrisonEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 219 | 232 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculateGarrisonEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 221 | 232 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1397 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1399 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1400 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowTrainer

Choice leaf: `perk.BowTrainer.choice`. Required skill: 125; alternative: `BowStrongBows`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2019`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowTrainer.primary | Daily Bow skill experience bonus to the party member with the lowest bow skill. | PartyLeader | 6f | Add | TroopUsageFlags.Undefined |
| perk.BowTrainer.secondary | {VALUE} daily experience to archers in your party. | PartyLeader | 3f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 64 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 66 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetPerkExperiencesForTroops](../source-paths/DefaultPartyTrainingModel.md) | 104 | 109 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowStrongBows

Choice leaf: `perk.BowStrongBows.choice`. Required skill: 125; alternative: `BowTrainer`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2020`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowStrongBows.primary | {VALUE}% damage with bows. | Personal | 0.08f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowStrongBows.secondary | {VALUE}% damage with bows by tier 3+ troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.BowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 244 | 413 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 247 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowDiscipline

Choice leaf: `perk.BowDiscipline.choice`. Required skill: 150; alternative: `BowHunterClan`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2021`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowDiscipline.primary | {VALUE}% aiming duration without losing accuracy. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowDiscipline.secondary | {VALUE} loyalty per day in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToGovernorPerks](../source-paths/DefaultSettlementLoyaltyModel.md) | 130 | 167 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1415 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1416 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowHunterClan

Choice leaf: `perk.BowHunterClan.choice`. Required skill: 150; alternative: `BowDiscipline`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2022`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowHunterClan.primary | {VALUE}% damage with bows to mounts. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowHunterClan.secondary | {VALUE}% garrison wages in the governed castle. | Governor | -0.15f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 140 | 204 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 251 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowSkirmishPhaseMaster

Choice leaf: `perk.BowSkirmishPhaseMaster.choice`. Required skill: 175; alternative: `BowEagleEye`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2023`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowSkirmishPhaseMaster.primary | {VALUE}% damage taken from projectiles. | Personal | -0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowSkirmishPhaseMaster.secondary | {VALUE}% damage taken from projectiles by ranged troops in your formation. | Captain | -0.1f | AddFactor | TroopUsageFlags.Ranged |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 520 | 595 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 524 | 595 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowEagleEye

Choice leaf: `perk.BowEagleEye.choice`. Required skill: 175; alternative: `BowSkirmishPhaseMaster`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2024`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowEagleEye.primary | {VALUE}% zoom with bows. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowEagleEye.secondary | {VALUE}% visual range on the campaign map. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetPartySpottingRange](../source-paths/DefaultMapVisibilityModel.md) | 37 | 84 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetMaxCameraZoom](../source-paths/SandboxAgentStatCalculateModel.md) | 550 | 563 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowBullsEye

Choice leaf: `perk.BowBullsEye.choice`. Required skill: 200; alternative: `BowRenownedArcher`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2025`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowBullsEye.primary | {VALUE}% bonus experience to ranged troops in your party after every battle. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Ranged |
| perk.BowBullsEye.secondary | {VALUE} daily experience to garrison troops in the governed settlement. | Governor | 3f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.GetBattleXpBonusFromPerks](../source-paths/DefaultCombatXpModel.md) | 106 | 121 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 40 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 42 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetPerkExperiencesForTroops](../source-paths/DefaultPartyTrainingModel.md) | 104 | 109 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowRenownedArcher

Choice leaf: `perk.BowRenownedArcher.choice`. Required skill: 200; alternative: `BowBullsEye`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2026`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowRenownedArcher.primary | {VALUE}% starting battle morale to ranged troops in your party. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowRenownedArcher.secondary | {VALUE}% recruitment and upgrade cost to ranged troops. | PartyLeader | -0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel.GetGoldCostForUpgrade](../source-paths/DefaultPartyTroopUpgradeModel.md) | 87 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel.GetGoldCostForUpgrade](../source-paths/DefaultPartyTroopUpgradeModel.md) | 89 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 256 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 258 | 287 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowHorseMaster

Choice leaf: `perk.BowHorseMaster.choice`. Required skill: 225; alternative: `BowDeepQuivers`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2027`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowHorseMaster.primary | You can now use all bows on horseback. | Personal | 0f | Invalid | TroopUsageFlags.Undefined |
| perk.BowHorseMaster.secondary | {VALUE} bow skill to horse archers in your formation | Captain | 30f | Add | TroopUsageFlags.Mounted &#124; TroopUsageFlags.BowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeAgentStats](../source-paths/SandboxAgentStatCalculateModel.md) | 70 | 91 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveSkill](../source-paths/SandboxAgentStatCalculateModel.md) | 264 | 320 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowDeepQuivers

Choice leaf: `perk.BowDeepQuivers.choice`. Required skill: 225; alternative: `BowHorseMaster`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2028`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowDeepQuivers.primary | {VALUE} extra arrows per quiver. | Personal | 3f | Add | TroopUsageFlags.Undefined |
| perk.BowDeepQuivers.secondary | {VALUE} extra arrow per quiver to troops in your party. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeMissionEquipment](../source-paths/SandboxAgentStatCalculateModel.md) | 155 | 202 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeMissionEquipment](../source-paths/SandboxAgentStatCalculateModel.md) | 156 | 202 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeMissionEquipment](../source-paths/SandboxAgentStatCalculateModel.md) | 158 | 202 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowQuickDraw

Choice leaf: `perk.BowQuickDraw.choice`. Required skill: 250; alternative: `BowRangersSwiftness`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2029`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowQuickDraw.primary | {VALUE}% aiming speed with bows. | Personal | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowQuickDraw.secondary | {VALUE}% tax gain in the governed settlement. | Governor | 0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel.CalculateDailyTaxInternal](../source-paths/DefaultSettlementTaxModel.md) | 83 | 112 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel.CalculateDailyTaxInternal](../source-paths/DefaultSettlementTaxModel.md) | 85 | 112 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1417 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowRangersSwiftness

Choice leaf: `perk.BowRangersSwiftness.choice`. Required skill: 250; alternative: `BowQuickDraw`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2030`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowRangersSwiftness.primary | Equipped bows do not slow you down. | Personal | 0f | Invalid | TroopUsageFlags.Undefined |
| perk.BowRangersSwiftness.secondary | {VALUE}% security provided by archers in the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculateGarrisonEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 223 | 232 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculateGarrisonEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 225 | 232 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.UpdateHumanStats](../source-paths/SandboxAgentStatCalculateModel.md) | 923 | 1128 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## BowDeadshot

Choice leaf: `perk.BowDeadshot.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2031`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.BowDeadshot.primary | {VALUE}% reload speed with bows for every skill point above 200. | Personal | 0.002f | AddFactor | TroopUsageFlags.Undefined |
| perk.BowDeadshot.secondary | {VALUE}% damage with bows for every skill point above 200. | Personal | 0.005f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 253 | 413 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1426 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
