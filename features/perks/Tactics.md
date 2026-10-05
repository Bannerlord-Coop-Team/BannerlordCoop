# Tactics perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## TacticsTightFormations

Choice leaf: `perk.TacticsTightFormations.choice`. Required skill: 25; alternative: `TacticsLooseFormations`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2156`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsTightFormations.primary | {VALUE}% damage by your infantry to cavalry when troops are sent to confront the enemy. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsTightFormations.secondary | {VALUE}% morale penalty when troops in your formation use shield wall, square, skein, column formations. | Captain | -0.25f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 49 | 119 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsLooseFormations

Choice leaf: `perk.TacticsLooseFormations.choice`. Required skill: 25; alternative: `TacticsTightFormations`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2157`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsLooseFormations.primary | {VALUE}% damage to your infantry from ranged troops when troops are sent to confront the enemy. | PartyLeader | -0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsLooseFormations.secondary | {VALUE}% morale penalty when troops in your formation use line, loose, circle or scatter formations. | Captain | -0.25f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 51 | 119 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 53 | 119 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsExtendedSkirmish

Choice leaf: `perk.TacticsExtendedSkirmish.choice`. Required skill: 50; alternative: `TacticsDecisiveBattle`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2158`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsExtendedSkirmish.primary | {VALUE}% damage in snowy and forest terrains when troops are sent to confront the enemy. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsExtendedSkirmish.secondary | {VALUE}% movement speed to troops in your formation in snowy and forest terrains. | Captain | 0.02f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 58 | 119 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1565 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsDecisiveBattle

Choice leaf: `perk.TacticsDecisiveBattle.choice`. Required skill: 50; alternative: `TacticsExtendedSkirmish`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2159`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsDecisiveBattle.primary | {VALUE}% damage in plains, steppes and deserts when your troops are sent to confront the enemy. | PartyLeader | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsDecisiveBattle.secondary | {VALUE}% movement speed to troops in your formation in plains, steppes and deserts. | Captain | 0.05f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 62 | 119 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1569 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsSmallUnitTactics

Choice leaf: `perk.TacticsSmallUnitTactics.choice`. Required skill: 75; alternative: `TacticsHordeLeader`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2160`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsSmallUnitTactics.primary | {VALUE} troop for the hideout crew | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |
| perk.TacticsSmallUnitTactics.secondary | {VALUE}% movement speed to troops in your formation when there are less than 15 soldiers. | Captain | 0.05f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel.GetMaximumTroopCountForHideoutMission](../source-paths/DefaultBanditDensityModel.md) | 69 | 74 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel.GetMaximumTroopCountForHideoutMission](../source-paths/DefaultBanditDensityModel.md) | 71 | 74 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1578 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsHordeLeader

Choice leaf: `perk.TacticsHordeLeader.choice`. Required skill: 75; alternative: `TacticsSmallUnitTactics`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2161`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsHordeLeader.primary | {VALUE} party size. | PartyLeader | 10f | Add | TroopUsageFlags.Undefined |
| perk.TacticsHordeLeader.secondary | {VALUE}% army cohesion loss to commanded armies. | ArmyCommander | -0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 319 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 321 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultArmyManagementCalculationModel.CalculateDailyCohesionChange](../source-paths/DefaultArmyManagementCalculationModel.md) | 235 | 242 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsLawkeeper

Choice leaf: `perk.TacticsLawkeeper.choice`. Required skill: 100; alternative: `TacticsCoaching`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2162`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsLawkeeper.primary | {VALUE}% damage against bandits when your troops are sent to confront the enemy. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsLawkeeper.secondary | {VALUE}% damage against bandits by troops in your formation. | Captain | 0.04f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 66 | 119 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 401 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsCoaching

Choice leaf: `perk.TacticsCoaching.choice`. Required skill: 100; alternative: `TacticsLawkeeper`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2163`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsCoaching.primary | {VALUE}% damage when your troops are sent to confront the enemy. | PartyLeader | 0.03f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsCoaching.secondary | {VALUE}% damage by troops in your formation. | Captain | 0.01f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 70 | 119 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 398 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsSwiftRegroup

Choice leaf: `perk.TacticsSwiftRegroup.choice`. Required skill: 125; alternative: `TacticsImproviser`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2164`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsSwiftRegroup.primary | {VALUE}% disorganized state duration when a raid or siege is broken. | PartyMember | -0.15f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsSwiftRegroup.secondary | {VALUE}% troops left behind when escaping from battles. | PartyLeader | -0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyImpairmentModel.GetDisorganizedStateDuration](../source-paths/DefaultPartyImpairmentModel.md) | 28 | 34 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyImpairmentModel.GetDisorganizedStateDuration](../source-paths/DefaultPartyImpairmentModel.md) | 30 | 34 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTroopSacrificeModel.GetNumberOfTroopsSacrificedForTryingToGetAway](../source-paths/DefaultTroopSacrificeModel.md) | 55 | 62 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsImproviser

Choice leaf: `perk.TacticsImproviser.choice`. Required skill: 125; alternative: `TacticsSwiftRegroup`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2165`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsImproviser.primary | No morale penalty for disorganized state in battles, in sally out or when being attacked. | PartyMember | 0f | Add | TroopUsageFlags.Undefined |
| perk.TacticsImproviser.secondary | {VALUE}% loss of troops when breaking into or out of a settlement under siege. | PartyLeader | -0.25f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTroopSacrificeModel.GetLostTroopCount](../source-paths/DefaultTroopSacrificeModel.md) | 96 | 98 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsOnTheMarch

Choice leaf: `perk.TacticsOnTheMarch.choice`. Required skill: 150; alternative: `TacticsCallToArms`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2166`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsOnTheMarch.primary | {VALUE}% fortification bonus to enemies when troops are sent to confront the enemy. | ArmyCommander | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsOnTheMarch.secondary | {VALUE}% fortification bonus to the governed settlement | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSettlementAdvantagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 231 | 239 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSettlementAdvantagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 233 | 239 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSettlementAdvantagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 235 | 239 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSettlementAdvantagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 237 | 239 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsCallToArms

Choice leaf: `perk.TacticsCallToArms.choice`. Required skill: 150; alternative: `TacticsOnTheMarch`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2167`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsCallToArms.primary | {VALUE}% movement speed to parties called to your army. | ArmyCommander | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsCallToArms.secondary | {VALUE}% influence required to call parties to your army | ArmyCommander | -0.15f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 353 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 355 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultArmyManagementCalculationModel.CalculatePartyInfluenceCost](../source-paths/DefaultArmyManagementCalculationModel.md) | 103 | 117 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultArmyManagementCalculationModel.CalculatePartyInfluenceCost](../source-paths/DefaultArmyManagementCalculationModel.md) | 105 | 117 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsPickThemOfTheWalls

Choice leaf: `perk.TacticsPickThemOfTheWalls.choice`. Required skill: 175; alternative: `TacticsMakeThemPay`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2168`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsPickThemOfTheWalls.primary | {VALUE}% chance for dealing double damage to siege defender troops in siege bombardment | Engineer | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsPickThemOfTheWalls.secondary | {VALUE}% chance for dealing double damage to besieging troops in siege bombardment of the governed settlement. | Governor | 0.25f | AddFactor | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsMakeThemPay

Choice leaf: `perk.TacticsMakeThemPay.choice`. Required skill: 175; alternative: `TacticsPickThemOfTheWalls`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2169`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsMakeThemPay.primary | {VALUE}% damage to defender siege engines. | Engineer | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsMakeThemPay.secondary | {VALUE}% damage to besieging siege engines. | Governor | 0.25f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineDamage](../source-paths/DefaultSiegeEventModel.md) | 487 | 511 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineDamage](../source-paths/DefaultSiegeEventModel.md) | 489 | 511 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineDamage](../source-paths/DefaultSiegeEventModel.md) | 505 | 511 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineDamage](../source-paths/DefaultSiegeEventModel.md) | 507 | 511 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsEliteReserves

Choice leaf: `perk.TacticsEliteReserves.choice`. Required skill: 200; alternative: `TacticsEncirclement`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2170`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsEliteReserves.primary | {VALUE}% less damage to tier 3+ units when troops are sent to confront the enemy. | PartyLeader | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsEliteReserves.secondary | {VALUE}% damage taken by troops in your formation. | Captain | -0.05f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 74 | 119 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageReductions](../source-paths/SandboxAgentApplyDamageModel.md) | 592 | 595 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsEncirclement

Choice leaf: `perk.TacticsEncirclement.choice`. Required skill: 200; alternative: `TacticsEliteReserves`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2171`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsEncirclement.primary | {VALUE}% damage to outnumbered enemies when troops are sent to confront the enemy. | PartyLeader | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsEncirclement.secondary | {VALUE}% influence cost to boost army cohesion. | ArmyCommander | -0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultArmyManagementCalculationModel.CalculateTotalInfluenceCostInternal](../source-paths/DefaultArmyManagementCalculationModel.md) | 219 | 224 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultArmyManagementCalculationModel.CalculateTotalInfluenceCostInternal](../source-paths/DefaultArmyManagementCalculationModel.md) | 221 | 224 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 78 | 119 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsPreBattleManeuvers

Choice leaf: `perk.TacticsPreBattleManeuvers.choice`. Required skill: 225; alternative: `TacticsBesieged`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2172`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsPreBattleManeuvers.primary | {VALUE}% influence gain from winning battles. | PartyMember | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsPreBattleManeuvers.secondary | {VALUE}% damage per 100 skill difference with the enemy when troops are sent to confront the enemy. | PartyLeader | 0.01f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.GetPartyBattleAdvantage](../source-paths/DefaultCombatSimulationModel.md) | 306 | 315 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsBesieged

Choice leaf: `perk.TacticsBesieged.choice`. Required skill: 225; alternative: `TacticsPreBattleManeuvers`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2173`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsBesieged.primary | {VALUE}% damage while besieged when troops are sent to confront the enemy. | PartyMember | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsBesieged.secondary | {VALUE}% influence gain from winning sieges. | Personal | 0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 95 | 119 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 97 | 119 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsCounteroffensive

Choice leaf: `perk.TacticsCounteroffensive.choice`. Required skill: 250; alternative: `TacticsGensdarmes`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2174`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsCounteroffensive.primary | {VALUE}% damage when troops are sent to confront the attacking enemy in a field battle. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.TacticsCounteroffensive.secondary | {VALUE}% damage when troops are sent to confront the enemy while outnumbered. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 82 | 119 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 113 | 119 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsGensdarmes

Choice leaf: `perk.TacticsGensdarmes.choice`. Required skill: 250; alternative: `TacticsCounteroffensive`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2175`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsGensdarmes.primary | {VALUE}% damage to infantry by cavalry troops in your formation. | Captain | 0.02f | AddFactor | TroopUsageFlags.Mounted |
| perk.TacticsGensdarmes.secondary | {VALUE} daily security in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementSecurityModel.CalculatePerkEffectsOnSecurity](../source-paths/DefaultSettlementSecurityModel.md) | 286 | 287 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 405 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TacticsTacticalMastery

Choice leaf: `perk.TacticsTacticalMastery.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2176`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TacticsTacticalMastery.primary | {VALUE}% damage for every skill point above 200 tactics skill when troops are sent to confront the enemy. | ArmyCommander | 0.005f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 117 | 119 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
