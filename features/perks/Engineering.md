# Engineering perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## EngineeringScaffolds

Choice leaf: `perk.EngineeringScaffolds.choice`. Required skill: 25; alternative: `EngineeringTorsionEngines`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2304`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringScaffolds.primary | {VALUE}% build speed to non-ranged siege engines. | Engineer | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringScaffolds.secondary | {VALUE}% shield hitpoints. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetConstructionProgressPerHour](../source-paths/DefaultSiegeEventModel.md) | 384 | 401 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeMissionEquipment](../source-paths/SandboxAgentStatCalculateModel.md) | 194 | 202 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringTorsionEngines

Choice leaf: `perk.EngineeringTorsionEngines.choice`. Required skill: 25; alternative: `EngineeringScaffolds`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2305`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringTorsionEngines.primary | {VALUE}% build speed to ranged siege engines. | Engineer | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringTorsionEngines.secondary | {VALUE} damage to equipped crossbows. | Personal | 3f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetConstructionProgressPerHour](../source-paths/DefaultSiegeEventModel.md) | 384 | 401 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 262 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringSiegeWorks

Choice leaf: `perk.EngineeringSiegeWorks.choice`. Required skill: 50; alternative: `EngineeringDungeonArchitect`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2306`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringSiegeWorks.primary | {VALUE}% hit points to ranged siege engines. | Engineer | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringSiegeWorks.secondary | {VALUE} prebuilt catapult to the settlement when a siege starts in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetPrebuiltSiegeEnginesOfSettlement](../source-paths/DefaultSiegeEventModel.md) | 434 | 440 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitPoints](../source-paths/DefaultSiegeEventModel.md) | 463 | 473 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitPoints](../source-paths/DefaultSiegeEventModel.md) | 465 | 473 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringDungeonArchitect

Choice leaf: `perk.EngineeringDungeonArchitect.choice`. Required skill: 50; alternative: `EngineeringSiegeWorks`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2307`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringDungeonArchitect.primary | {VALUE}% chance of ranged siege engines getting hit while under bombardment. | Engineer | -0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringDungeonArchitect.secondary | {VALUE}% escape chance to prisoners in dungeons of governed settlements. | Governor | -0.25f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitChance](../source-paths/DefaultSiegeEventModel.md) | 323 | 332 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitChance](../source-paths/DefaultSiegeEventModel.md) | 325 | 332 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringCarpenters

Choice leaf: `perk.EngineeringCarpenters.choice`. Required skill: 75; alternative: `EngineeringMilitaryPlanner`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2308`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringCarpenters.primary | {VALUE}% hit points to rams and siege-towers. | Engineer | 0.33f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringCarpenters.secondary | {VALUE}% build speed for projects in the governed town. | Governor | 0.12f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 115 | 160 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 117 | 160 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitPoints](../source-paths/DefaultSiegeEventModel.md) | 468 | 473 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitPoints](../source-paths/DefaultSiegeEventModel.md) | 470 | 473 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringMilitaryPlanner

Choice leaf: `perk.EngineeringMilitaryPlanner.choice`. Required skill: 75; alternative: `EngineeringCarpenters`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2309`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringMilitaryPlanner.primary | {VALUE}% ammunition to ranged troops when besieging. | Engineer | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringMilitaryPlanner.secondary | {VALUE}% build speed for projects in the governed castle. | Governor | 0.25f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 111 | 160 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 113 | 160 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.InitializeMissionEquipment](../source-paths/SandboxAgentStatCalculateModel.md) | 183 | 202 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringWallBreaker

Choice leaf: `perk.EngineeringWallBreaker.choice`. Required skill: 100; alternative: `EngineeringDreadfulSieger`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2310`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringWallBreaker.primary | {VALUE}% damage dealt to walls during siege bombardment. | Engineer | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringWallBreaker.secondary | {VALUE}% damage dealt to shields by troops in your formation. | Captain | 0.1f | AddFactor | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineDamage](../source-paths/DefaultSiegeEventModel.md) | 483 | 511 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineDamage](../source-paths/DefaultSiegeEventModel.md) | 485 | 511 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 389 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringDreadfulSieger

Choice leaf: `perk.EngineeringDreadfulSieger.choice`. Required skill: 100; alternative: `EngineeringWallBreaker`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2311`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringDreadfulSieger.primary | {VALUE}% accuracy to your siege engines during siege bombardments in the governed settlement. | Governor | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringDreadfulSieger.secondary | {VALUE}% crossbow damage by troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.CrossbowUser |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitChance](../source-paths/DefaultSiegeEventModel.md) | 300 | 332 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitChance](../source-paths/DefaultSiegeEventModel.md) | 302 | 332 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 277 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringSalvager

Choice leaf: `perk.EngineeringSalvager.choice`. Required skill: 125; alternative: `EngineeringForeman`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2312`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringSalvager.primary | {VALUE}% accuracy to ballistas during siege bombardment. | Engineer | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringSalvager.secondary | {VALUE}% siege engine build speed increase for each militia. | Governor | 0.001f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitChance](../source-paths/DefaultSiegeEventModel.md) | 318 | 332 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitChance](../source-paths/DefaultSiegeEventModel.md) | 320 | 332 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetConstructionProgressPerHour](../source-paths/DefaultSiegeEventModel.md) | 394 | 401 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringForeman

Choice leaf: `perk.EngineeringForeman.choice`. Required skill: 125; alternative: `EngineeringSalvager`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2313`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringForeman.primary | {VALUE}% mangonel and trebuchet accuracy during siege bombardment. | Engineer | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringForeman.secondary | {VALUE} prosperity when a project is finished in the governed settlement. | Governor | 100f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitChance](../source-paths/DefaultSiegeEventModel.md) | 314 | 332 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitChance](../source-paths/DefaultSiegeEventModel.md) | 316 | 332 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringStonecutters

Choice leaf: `perk.EngineeringStonecutters.choice`. Required skill: 150; alternative: `EngineeringSiegeEngineer`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2314`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringStonecutters.primary | {VALUE}% build speed for fortifications, aqueducts and barrack projects in the governed settlement. | Governor | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringStonecutters.secondary | Fire versions of siege engines can be constructed. | Engineer | 0f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 120 | 160 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 122 | 160 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetAvailableAttackerRangedSiegeEngines](../source-paths/DefaultSiegeEventModel.md) | 533 | 545 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetAvailableDefenderSiegeEngines](../source-paths/DefaultSiegeEventModel.md) | 549 | 560 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringSiegeEngineer

Choice leaf: `perk.EngineeringSiegeEngineer.choice`. Required skill: 150; alternative: `EngineeringStonecutters`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2315`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringSiegeEngineer.primary | {VALUE}% hit points to defensive siege engines in the governed settlement. | Governor | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringSiegeEngineer.secondary | Fire versions of siege engines can be constructed. | Engineer | 0f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitPoints](../source-paths/DefaultSiegeEventModel.md) | 457 | 473 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineHitPoints](../source-paths/DefaultSiegeEventModel.md) | 459 | 473 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetAvailableAttackerRangedSiegeEngines](../source-paths/DefaultSiegeEventModel.md) | 533 | 545 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetAvailableDefenderSiegeEngines](../source-paths/DefaultSiegeEventModel.md) | 549 | 560 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringCampBuilding

Choice leaf: `perk.EngineeringCampBuilding.choice`. Required skill: 175; alternative: `EngineeringBattlements`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2316`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringCampBuilding.primary | {VALUE}% cohesion loss of armies when besieging. | ArmyCommander | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringCampBuilding.secondary | {VALUE}% casualty chance from siege bombardments. | Engineer | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultArmyManagementCalculationModel.CalculateDailyCohesionChange](../source-paths/DefaultArmyManagementCalculationModel.md) | 237 | 242 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultArmyManagementCalculationModel.CalculateDailyCohesionChange](../source-paths/DefaultArmyManagementCalculationModel.md) | 239 | 242 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetCasualtyChance](../source-paths/DefaultSiegeEventModel.md) | 235 | 248 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetCasualtyChance](../source-paths/DefaultSiegeEventModel.md) | 237 | 248 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringBattlements

Choice leaf: `perk.EngineeringBattlements.choice`. Required skill: 175; alternative: `EngineeringCampBuilding`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2317`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringBattlements.primary | {VALUE} prebuilt ballista when you set up a siege camp. | Engineer | 1f | Add | TroopUsageFlags.Undefined |
| perk.EngineeringBattlements.secondary | {VALUE} maximum food reserve limits in the governed settlement. | Governor | 100f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingEffectModel.GetBuildingEffect](../source-paths/DefaultBuildingEffectModel.md) | 26 | 38 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetPrebuiltSiegeEnginesOfSiegeCamp](../source-paths/DefaultSiegeEventModel.md) | 445 | 450 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringEngineeringGuilds

Choice leaf: `perk.EngineeringEngineeringGuilds.choice`. Required skill: 200; alternative: `EngineeringApprenticeship`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2318`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringEngineeringGuilds.primary | {VALUE} recruitment slot when recruiting from artisan notables. | Engineer | 1f | Add | TroopUsageFlags.Undefined |
| perk.EngineeringEngineeringGuilds.secondary | {VALUE}% wall hit points in the governed settlement. | Governor | 0.25f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero](../source-paths/DefaultVolunteerModel.md) | 42 | 47 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero](../source-paths/DefaultVolunteerModel.md) | 44 | 47 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultWallHitPointCalculationModel.CalculateMaximumWallHitPointInternal](../source-paths/DefaultWallHitPointCalculationModel.md) | 39 | 44 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultWallHitPointCalculationModel.CalculateMaximumWallHitPointInternal](../source-paths/DefaultWallHitPointCalculationModel.md) | 41 | 44 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringApprenticeship

Choice leaf: `perk.EngineeringApprenticeship.choice`. Required skill: 200; alternative: `EngineeringEngineeringGuilds`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2319`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringApprenticeship.primary | {VALUE} experience to troops when a siege engine is built. | Engineer | 5f | Add | TroopUsageFlags.Undefined |
| perk.EngineeringApprenticeship.secondary | {VALUE}% prosperity gain for each unique project in the governed settlement. | Governor | 0.01f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementProsperityModel.CalculateProsperityChangeInternal](../source-paths/DefaultSettlementProsperityModel.md) | 147 | 200 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementProsperityModel.CalculateProsperityChangeInternal](../source-paths/DefaultSettlementProsperityModel.md) | 153 | 200 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementProsperityModel.CalculateProsperityChangeInternal](../source-paths/DefaultSettlementProsperityModel.md) | 157 | 200 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringMetallurgy

Choice leaf: `perk.EngineeringMetallurgy.choice`. Required skill: 225; alternative: `EngineeringImprovedTools`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2320`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringMetallurgy.primary | {VALUE}% chance to remove negative modifiers on looted items. | Engineer | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringMetallurgy.secondary | {VALUE} armor to all equipped armor pieces of troops in your formation. | Captain | 5f | Add | TroopUsageFlags.None |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.GetLootedItemFromTroop](../source-paths/DefaultBattleRewardModel.md) | 111 | 118 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBattleRewardModel.GetLootedItemFromTroop](../source-paths/DefaultBattleRewardModel.md) | 113 | 118 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1541 | 1586 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1543 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringImprovedTools

Choice leaf: `perk.EngineeringImprovedTools.choice`. Required skill: 225; alternative: `EngineeringMetallurgy`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2321`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringImprovedTools.primary | {VALUE}% siege camp preparation speed. | Engineer | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringImprovedTools.secondary | {VALUE}% melee damage by troops in your formation. | Captain | 0.05f | AddFactor | TroopUsageFlags.Melee |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetConstructionProgressPerHour](../source-paths/DefaultSiegeEventModel.md) | 378 | 401 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetConstructionProgressPerHour](../source-paths/DefaultSiegeEventModel.md) | 380 | 401 |
| [SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications](../source-paths/SandboxAgentApplyDamageModel.md) | 203 | 413 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringClockwork

Choice leaf: `perk.EngineeringClockwork.choice`. Required skill: 250; alternative: `EngineeringArchitecturalCommissions`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2322`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringClockwork.primary | {VALUE}% reload speed to ballistas during siege bombardment. | Engineer | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringClockwork.secondary | {VALUE}% effect from boosting projects in the governed town. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 92 | 160 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 94 | 160 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetRangedSiegeEngineReloadTime](../source-paths/DefaultSiegeEventModel.md) | 519 | 529 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetRangedSiegeEngineReloadTime](../source-paths/DefaultSiegeEventModel.md) | 521 | 529 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringArchitecturalCommissions

Choice leaf: `perk.EngineeringArchitecturalCommissions.choice`. Required skill: 250; alternative: `EngineeringClockwork`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2323`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringArchitecturalCommissions.primary | {VALUE}% reload speed to mangonels and trebuchets in siege bombardment. | Engineer | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.EngineeringArchitecturalCommissions.secondary | {VALUE} gold per day for continuous projects in the governed settlement. | Governor | 20f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateTownIncomeFromProjects](../source-paths/DefaultClanFinanceModel.md) | 462 | 468 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateTownIncomeFromProjects](../source-paths/DefaultClanFinanceModel.md) | 464 | 468 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetRangedSiegeEngineReloadTime](../source-paths/DefaultSiegeEventModel.md) | 523 | 529 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetRangedSiegeEngineReloadTime](../source-paths/DefaultSiegeEventModel.md) | 525 | 529 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## EngineeringMasterwork

Choice leaf: `perk.EngineeringMasterwork.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2324`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.EngineeringMasterwork.primary | {VALUE}% damage for each engineering skill point over 250 for siege engines in siege bombardment. | Engineer | 0.01f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineDamage](../source-paths/DefaultSiegeEventModel.md) | 492 | 511 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineDamage](../source-paths/DefaultSiegeEventModel.md) | 497 | 511 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetSiegeEngineDamage](../source-paths/DefaultSiegeEventModel.md) | 498 | 511 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
