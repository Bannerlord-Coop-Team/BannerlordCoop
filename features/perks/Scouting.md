# Scouting perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## ScoutingDayTraveler

Choice leaf: `perk.ScoutingDayTraveler.choice`. Required skill: 25; alternative: `ScoutingNightRunner`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2135`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingDayTraveler.primary | {VALUE}% travel speed during daytime. | Scout | 0.02f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingDayTraveler.secondary | {VALUE}% sight range during daytime in campaign map. | Scout | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 329 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 331 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetPartySpottingRange](../source-paths/DefaultMapVisibilityModel.md) | 59 | 84 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingNightRunner

Choice leaf: `perk.ScoutingNightRunner.choice`. Required skill: 25; alternative: `ScoutingDayTraveler`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2136`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingNightRunner.primary | {VALUE}% travel speed during nighttime | Scout | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingNightRunner.secondary | {VALUE}% sight range during nighttime in campaign map. | Scout | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 324 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 326 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetPartySpottingRange](../source-paths/DefaultMapVisibilityModel.md) | 55 | 84 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingPathfinder

Choice leaf: `perk.ScoutingPathfinder.choice`. Required skill: 50; alternative: `ScoutingWaterDiviner`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2137`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingPathfinder.primary | {VALUE}% travel speed on steppes and plains. | Scout | 0.02f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingPathfinder.secondary | {VALUE}% daily chance to increase relation with a notable by 1 when you enter a town. | PartyLeader | 0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 306 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 308 | 359 |
| [TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior.CheckPerkAndGiveRelation](../source-paths/PlayerTownVisitCampaignBehavior.md) | 1472 | 1490 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingWaterDiviner

Choice leaf: `perk.ScoutingWaterDiviner.choice`. Required skill: 50; alternative: `ScoutingPathfinder`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2138`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingWaterDiviner.primary | {VALUE}% sight range while traveling on steppes and plains. | Scout | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingWaterDiviner.secondary | {VALUE}% daily chance to increase relation with a notable by 1 when you enter a village. | PartyLeader | 0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetPartySpottingRange](../source-paths/DefaultMapVisibilityModel.md) | 49 | 84 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetPartySpottingRange](../source-paths/DefaultMapVisibilityModel.md) | 51 | 84 |
| [TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior.CheckPerkAndGiveRelation](../source-paths/PlayerTownVisitCampaignBehavior.md) | 1472 | 1490 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingForestKin

Choice leaf: `perk.ScoutingForestKin.choice`. Required skill: 75; alternative: `ScoutingDesertBorn`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2139`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingForestKin.primary | {VALUE}% travel speed penalty from forests if your party is composed of 75% or more infantry units. | Scout | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingForestKin.secondary | {VALUE}% tax income from villages bound to the governed settlement. | Governor | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 267 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 277 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateVillageIncome](../source-paths/DefaultClanFinanceModel.md) | 478 | 495 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateVillageIncome](../source-paths/DefaultClanFinanceModel.md) | 480 | 495 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingDesertBorn

Choice leaf: `perk.ScoutingDesertBorn.choice`. Required skill: 75; alternative: `ScoutingForestKin`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2140`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingDesertBorn.primary | {VALUE}% travel speed on deserts and dunes. | Scout | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingDesertBorn.secondary | {VALUE}% tax income from the governed settlement. | Governor | 0.025f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 299 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 301 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel.CalculateDailyTaxInternal](../source-paths/DefaultSettlementTaxModel.md) | 98 | 112 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel.CalculateDailyTaxInternal](../source-paths/DefaultSettlementTaxModel.md) | 100 | 112 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingForcedMarch

Choice leaf: `perk.ScoutingForcedMarch.choice`. Required skill: 100; alternative: `ScoutingUnburdened`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2141`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingForcedMarch.primary | {VALUE}% travel speed when the party morale is higher than 75. | Scout | 0.025f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingForcedMarch.secondary | {VALUE} experience per day to all troops while traveling with party morale higher than 75. | PartyLeader | 2f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 339 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 341 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 76 | 96 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingUnburdened

Choice leaf: `perk.ScoutingUnburdened.choice`. Required skill: 100; alternative: `ScoutingForcedMarch`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2142`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingUnburdened.primary | {VALUE}% overburden penalty. | Scout | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingUnburdened.secondary | {VALUE} experience per day to all troops when traveling while overburdened. | PartyLeader | 2f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.GetOverburdenedEffect](../source-paths/DefaultPartySpeedCalculatingModel.md) | 251 | 254 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 80 | 96 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingTracker

Choice leaf: `perk.ScoutingTracker.choice`. Required skill: 125; alternative: `ScoutingRanger`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2143`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingTracker.primary | {VALUE}% track visibility duration. | Scout | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingTracker.secondary | {VALUE}% travel speed while following a hostile party. | Scout | 0.02f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 347 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 349 | 359 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapTrackModel.GetTrackLife](../source-paths/DefaultMapTrackModel.md) | 47 | 52 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapTrackModel.GetTrackLife](../source-paths/DefaultMapTrackModel.md) | 49 | 52 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingRanger

Choice leaf: `perk.ScoutingRanger.choice`. Required skill: 125; alternative: `ScoutingTracker`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2144`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingRanger.primary | {VALUE}% track spotting distance. | Scout | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingRanger.secondary | {VALUE}% track detection chance. | Scout | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapTrackModel.GetMaxTrackSpottingDistanceForMainParty](../source-paths/DefaultMapTrackModel.md) | 28 | 31 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapTrackModel.GetTrackDetectionDifficultyForMainParty](../source-paths/DefaultMapTrackModel.md) | 60 | 65 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapTrackModel.GetTrackDetectionDifficultyForMainParty](../source-paths/DefaultMapTrackModel.md) | 62 | 65 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingMountedScouts

Choice leaf: `perk.ScoutingMountedScouts.choice`. Required skill: 150; alternative: `ScoutingPatrols`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2145`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingMountedScouts.primary | {VALUE}% sight range when your party is composed of more than %50 cavalry troops. | Scout | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingMountedScouts.secondary | {VALUE} party size limit. | PartyLeader | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 323 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.CalculateBaseMemberSize](../source-paths/DefaultPartySizeLimitModel.md) | 325 | 372 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetPartySpottingRange](../source-paths/DefaultMapVisibilityModel.md) | 66 | 84 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetPartySpottingRange](../source-paths/DefaultMapVisibilityModel.md) | 78 | 84 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingPatrols

Choice leaf: `perk.ScoutingPatrols.choice`. Required skill: 150; alternative: `ScoutingMountedScouts`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2146`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingPatrols.primary | {VALUE} battle morale against bandit parties. | Scout | 5f | Add | TroopUsageFlags.Undefined |
| perk.ScoutingPatrols.secondary | {VALUE}% advantage against bandits when troops are sent to confront the enemy. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.GetPartyBattleAdvantage](../source-paths/DefaultCombatSimulationModel.md) | 303 | 315 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingForagers

Choice leaf: `perk.ScoutingForagers.choice`. Required skill: 175; alternative: `ScoutingBeastWhisperer`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2147`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingForagers.primary | {VALUE}% food consumption while traveling through steppes and forests. | Scout | -0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingForagers.secondary | {VALUE}% disorganized state duration. | PartyLeader | -0.15f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 66 | 87 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyImpairmentModel.GetDisorganizedStateDuration](../source-paths/DefaultPartyImpairmentModel.md) | 32 | 34 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingBeastWhisperer

Choice leaf: `perk.ScoutingBeastWhisperer.choice`. Required skill: 175; alternative: `ScoutingForagers`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2148`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingBeastWhisperer.primary | {VALUE}% chance to find a mount when traveling through steppes and plains. | Scout | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingBeastWhisperer.secondary | {VALUE}% carrying capacity for pack animals in your party. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultInventoryCapacityModel.CalculateInventoryCapacity](../source-paths/DefaultInventoryCapacityModel.md) | 80 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultInventoryCapacityModel.CalculateInventoryCapacity](../source-paths/DefaultInventoryCapacityModel.md) | 82 | 100 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingVillageNetwork

Choice leaf: `perk.ScoutingVillageNetwork.choice`. Required skill: 200; alternative: `ScoutingRumourNetwork`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2149`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingVillageNetwork.primary | {VALUE}% trade penalty with villages of your own culture. | PartyLeader | -0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingVillageNetwork.secondary | {VALUE}% villager party size of villages bound to the governed settlement. | Governor | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 86 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty](../source-paths/DefaultPartySizeLimitModel.md) | 452 | 464 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty](../source-paths/DefaultPartySizeLimitModel.md) | 454 | 464 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingRumourNetwork

Choice leaf: `perk.ScoutingRumourNetwork.choice`. Required skill: 200; alternative: `ScoutingVillageNetwork`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2150`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingRumourNetwork.primary | {VALUE}% trade penalty within cities of your own kingdom. | PartyLeader | -0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingRumourNetwork.secondary | {VALUE}% hideout detection range. | Scout | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 90 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetHideoutSpottingDistance](../source-paths/DefaultMapVisibilityModel.md) | 104 | 109 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetHideoutSpottingDistance](../source-paths/DefaultMapVisibilityModel.md) | 106 | 109 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingVantagePoint

Choice leaf: `perk.ScoutingVantagePoint.choice`. Required skill: 225; alternative: `ScoutingKeenSight`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2151`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingVantagePoint.primary | {VALUE}% sight range when stationary for at least an hour. | Scout | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingVantagePoint.secondary | {VALUE} prisoner limit. | PartyLeader | 10f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.AddMobilePartyLeaderPrisonerSizePerkEffects](../source-paths/DefaultPartySizeLimitModel.md) | 225 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel.AddMobilePartyLeaderPrisonerSizePerkEffects](../source-paths/DefaultPartySizeLimitModel.md) | 227 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetPartySpottingRange](../source-paths/DefaultMapVisibilityModel.md) | 64 | 84 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingKeenSight

Choice leaf: `perk.ScoutingKeenSight.choice`. Required skill: 225; alternative: `ScoutingVantagePoint`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2152`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingKeenSight.primary | {VALUE}% sight penalty for traveling in forests. | Scout | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingKeenSight.secondary | {VALUE}% chance of prisoner lords escaping from your party. | PartyLeader | -0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange](../source-paths/DefaultMapVisibilityModel.md) | 92 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange](../source-paths/DefaultMapVisibilityModel.md) | 94 | 100 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingVanguard

Choice leaf: `perk.ScoutingVanguard.choice`. Required skill: 250; alternative: `ScoutingRearguard`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2153`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingVanguard.primary | {VALUE}% damage by your troops when they are sent as attackers. | PartyLeader | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingVanguard.secondary | {VALUE}% damage by your troops when they are sent to sally out. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 99 | 119 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 101 | 119 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 107 | 119 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 109 | 119 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingRearguard

Choice leaf: `perk.ScoutingRearguard.choice`. Required skill: 250; alternative: `ScoutingVanguard`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2154`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingRearguard.primary | {VALUE}% wounded troop recovery speed while in an army. | PartyLeader | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.ScoutingRearguard.secondary | {VALUE}% damage by your troops when defending at your siege camp. | PartyLeader | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 200 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCombatSimulationModel.CalculateSimulationDamagePerkEffects](../source-paths/DefaultCombatSimulationModel.md) | 105 | 119 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## ScoutingUncannyInsight

Choice leaf: `perk.ScoutingUncannyInsight.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2155`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.ScoutingUncannyInsight.primary | {VALUE}% party speed for every skill point above 200 scouting skill. | Scout | 0.001f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.CalculateFinalSpeed](../source-paths/DefaultPartySpeedCalculatingModel.md) | 338 | 359 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
