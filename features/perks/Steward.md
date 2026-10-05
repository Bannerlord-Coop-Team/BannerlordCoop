# Steward perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## StewardWarriorsDiet

Choice leaf: `perk.StewardWarriorsDiet.choice`. Required skill: 25; alternative: `StewardFrugal`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2262`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardWarriorsDiet.primary | {VALUE}% food consumption in your party. | Quartermaster | -0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardWarriorsDiet.secondary | No morale penalty from having single type of food. | PartyLeader | 0f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyMoraleModel.CalculateFoodVarietyMoraleBonus](../source-paths/DefaultPartyMoraleModel.md) | 113 | 130 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 57 | 87 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardFrugal

Choice leaf: `perk.StewardFrugal.choice`. Required skill: 25; alternative: `StewardWarriorsDiet`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2263`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardFrugal.primary | {VALUE}% wages in your party. | Quartermaster | -0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardFrugal.secondary | {VALUE}% recruitment costs. | PartyLeader | -0.15f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 183 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 185 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 269 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 271 | 287 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardSevenVeterans

Choice leaf: `perk.StewardSevenVeterans.choice`. Required skill: 50; alternative: `StewardDrillSergant`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2264`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardSevenVeterans.primary | {VALUE} daily experience for tier 4+ troops in your party. | Quartermaster | 4f | Add | TroopUsageFlags.Undefined |
| perk.StewardSevenVeterans.secondary | {VALUE}% rate of militias will spawn as veteran troops in the governed settlement. | Governor | 0.1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.CalculateVeteranMilitiaSpawnChance](../source-paths/DefaultSettlementMilitiaModel.md) | 68 | 86 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.CalculateVeteranMilitiaSpawnChance](../source-paths/DefaultSettlementMilitiaModel.md) | 70 | 86 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementMilitiaModel.GetSettlementMilitiaChangeDueToPerks](../source-paths/DefaultSettlementMilitiaModel.md) | 178 | 180 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 83 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 85 | 96 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardDrillSergant

Choice leaf: `perk.StewardDrillSergant.choice`. Required skill: 50; alternative: `StewardSevenVeterans`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2265`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardDrillSergant.primary | {VALUE} daily experience to troops in your party. | Quartermaster | 2f | Add | TroopUsageFlags.Undefined |
| perk.StewardDrillSergant.secondary | {VALUE}% garrison wages in the governed settlement. | Governor | -0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 130 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 87 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetEffectiveDailyExperience](../source-paths/DefaultPartyTrainingModel.md) | 89 | 96 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTrainingModel.GetPerkExperiencesForTroops](../source-paths/DefaultPartyTrainingModel.md) | 100 | 109 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardSweatshops

Choice leaf: `perk.StewardSweatshops.choice`. Required skill: 75; alternative: `StewardStiffUpperLip`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2266`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardSweatshops.primary | {VALUE}% production rate to owned workshops. | Personal | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardSweatshops.secondary | {VALUE}% siege engine build rate in your party. | Quartermaster | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetConstructionProgressPerHour](../source-paths/DefaultSiegeEventModel.md) | 371 | 401 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetConstructionProgressPerHour](../source-paths/DefaultSiegeEventModel.md) | 373 | 401 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultWorkshopModel.GetEffectiveConversionSpeedOfProduction](../source-paths/DefaultWorkshopModel.md) | 51 | 53 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardStiffUpperLip

Choice leaf: `perk.StewardStiffUpperLip.choice`. Required skill: 75; alternative: `StewardSweatshops`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2267`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardStiffUpperLip.primary | {VALUE}% food consumption in your party while it is part of an army. | Quartermaster | -0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardStiffUpperLip.secondary | {VALUE}% garrison wages in the governed castle. | Governor | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 141 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 74 | 87 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardPaidInPromise

Choice leaf: `perk.StewardPaidInPromise.choice`. Required skill: 100; alternative: `StewardEfficientCampaigner`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2268`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardPaidInPromise.primary | {VALUE}% companion wages and recruitment fees. | PartyLeader | -0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardPaidInPromise.secondary | Discarded armors are donated to troops for increased experience. | Quartermaster | 0f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 72 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCompanionHiringPriceCalculationModel.GetCompanionHiringPrice](../source-paths/DefaultCompanionHiringPriceCalculationModel.md) | 39 | 48 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCompanionHiringPriceCalculationModel.GetCompanionHiringPrice](../source-paths/DefaultCompanionHiringPriceCalculationModel.md) | 41 | 48 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultItemDiscardModel.PlayerCanDonateItem](../source-paths/DefaultItemDiscardModel.md) | 21 | 24 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardEfficientCampaigner

Choice leaf: `perk.StewardEfficientCampaigner.choice`. Required skill: 100; alternative: `StewardPaidInPromise`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2269`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardEfficientCampaigner.primary | {VALUE} extra food for each food taken during village raids for your party. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |
| perk.StewardEfficientCampaigner.secondary | {VALUE}% troop wages in your party while it is part of an army. | Quartermaster | -0.25f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 189 | 204 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardForeseeableFuture

Choice leaf: `perk.StewardForeseeableFuture.choice`. Required skill: 125; alternative: `StewardLogistician`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2270`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardForeseeableFuture.primary | Discarded weapons are donated to troops for increased experience. | Quartermaster | 0f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardForeseeableFuture.secondary | {VALUE}% tariff income in the governed settlement. | Governor | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateTownIncomeFromTariffs](../source-paths/DefaultClanFinanceModel.md) | 441 | 452 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultItemDiscardModel.PlayerCanDonateItem](../source-paths/DefaultItemDiscardModel.md) | 17 | 24 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardLogistician

Choice leaf: `perk.StewardLogistician.choice`. Required skill: 125; alternative: `StewardForeseeableFuture`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2271`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardLogistician.primary | {VALUE} party morale when number of mounts is greater than number of foot troops in your party. | Quartermaster | 4f | Add | TroopUsageFlags.Undefined |
| perk.StewardLogistician.secondary | {VALUE}% tax income. | Governor | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyMoraleModel.GetMoraleEffectsFromPerks](../source-paths/DefaultPartyMoraleModel.md) | 173 | 190 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyMoraleModel.GetMoraleEffectsFromPerks](../source-paths/DefaultPartyMoraleModel.md) | 188 | 190 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateVillageIncome](../source-paths/DefaultClanFinanceModel.md) | 482 | 495 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateVillageIncome](../source-paths/DefaultClanFinanceModel.md) | 484 | 495 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel.CalculateDailyTaxInternal](../source-paths/DefaultSettlementTaxModel.md) | 89 | 112 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel.CalculateDailyTaxInternal](../source-paths/DefaultSettlementTaxModel.md) | 91 | 112 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardRelocation

Choice leaf: `perk.StewardRelocation.choice`. Required skill: 150; alternative: `StewardAidCorps`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2272`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardRelocation.primary | {VALUE}% influence gain from donating troops. | Quartermaster | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardRelocation.secondary | {VALUE}% effect from boosting projects in the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.GetBoostAmount](../source-paths/DefaultBuildingConstructionModel.md) | 62 | 71 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.GetBoostAmount](../source-paths/DefaultBuildingConstructionModel.md) | 64 | 71 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerDonationModel.CalculateInfluenceGainAfterTroopDonation](../source-paths/DefaultPrisonerDonationModel.md) | 34 | 39 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonerDonationModel.CalculateInfluenceGainAfterTroopDonation](../source-paths/DefaultPrisonerDonationModel.md) | 36 | 39 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardAidCorps

Choice leaf: `perk.StewardAidCorps.choice`. Required skill: 150; alternative: `StewardRelocation`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2273`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardAidCorps.primary | Wounded troops in your party are no longer paid wages. | Quartermaster | 0f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardAidCorps.secondary | {VALUE}% hearth growth in villages bound to the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 51 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementProsperityModel.CalculateHearthChangeInternal](../source-paths/DefaultSettlementProsperityModel.md) | 59 | 70 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardGourmet

Choice leaf: `perk.StewardGourmet.choice`. Required skill: 175; alternative: `StewardSoundReserves`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2274`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardGourmet.primary | Double the morale bonus from having diverse food in your party. | Quartermaster | 1f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardGourmet.secondary | {VALUE}% garrison food consumption during sieges in the governed settlement. | Governor | -0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyMoraleModel.CalculateFoodVarietyMoraleBonus](../source-paths/DefaultPartyMoraleModel.md) | 122 | 130 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyMoraleModel.CalculateFoodVarietyMoraleBonus](../source-paths/DefaultPartyMoraleModel.md) | 128 | 130 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementFoodModel.CalculateTownFoodChangeInternal](../source-paths/DefaultSettlementFoodModel.md) | 51 | 97 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardSoundReserves

Choice leaf: `perk.StewardSoundReserves.choice`. Required skill: 175; alternative: `StewardGourmet`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2275`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardSoundReserves.primary | {VALUE}% troop upgrade costs. | Quartermaster | -0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardSoundReserves.secondary | {VALUE}% food consumption during sieges in your party. | Quartermaster | -0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel.GetGoldCostForUpgrade](../source-paths/DefaultPartyTroopUpgradeModel.md) | 83 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel.GetGoldCostForUpgrade](../source-paths/DefaultPartyTroopUpgradeModel.md) | 85 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 78 | 87 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 80 | 87 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardForcedLabor

Choice leaf: `perk.StewardForcedLabor.choice`. Required skill: 200; alternative: `StewardContractors`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2276`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardForcedLabor.primary | Prisoners in your party provide carry capacity as if they are standard troops. | Quartermaster | 0f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardForcedLabor.secondary | {VALUE}% construction speed per every 3 prisoners. | Governor | 0.01f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultInventoryCapacityModel.CalculateInventoryCapacity](../source-paths/DefaultInventoryCapacityModel.md) | 70 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 102 | 160 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 106 | 160 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 108 | 160 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 109 | 160 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardContractors

Choice leaf: `perk.StewardContractors.choice`. Required skill: 200; alternative: `StewardForcedLabor`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2277`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardContractors.primary | {VALUE}% wages and upgrade costs of the mercenary troops in your party. | Quartermaster | -0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardContractors.secondary | {VALUE}% town project effects in the governed settlement. | Governor | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel.GetGoldCostForUpgrade](../source-paths/DefaultPartyTroopUpgradeModel.md) | 95 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyTroopUpgradeModel.GetGoldCostForUpgrade](../source-paths/DefaultPartyTroopUpgradeModel.md) | 97 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 159 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 164 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 165 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingEffectModel.GetBuildingEffect](../source-paths/DefaultBuildingEffectModel.md) | 28 | 38 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardArenicosMules

Choice leaf: `perk.StewardArenicosMules.choice`. Required skill: 225; alternative: `StewardArenicosHorses`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2278`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardArenicosMules.primary | {VALUE}% carrying capacity for pack animals in your party. | Quartermaster | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardArenicosMules.secondary | {VALUE}% trade penalty for trading pack animals. | Quartermaster | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 120 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 122 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultInventoryCapacityModel.CalculateInventoryCapacity](../source-paths/DefaultInventoryCapacityModel.md) | 88 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultInventoryCapacityModel.CalculateInventoryCapacity](../source-paths/DefaultInventoryCapacityModel.md) | 90 | 100 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardArenicosHorses

Choice leaf: `perk.StewardArenicosHorses.choice`. Required skill: 225; alternative: `StewardArenicosMules`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2279`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardArenicosHorses.primary | {VALUE}% carrying capacity for troops in your party. | Quartermaster | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardArenicosHorses.secondary | {VALUE}% trade penalty for trading mounts. | Personal | -0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 130 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 132 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultInventoryCapacityModel.CalculateInventoryCapacity](../source-paths/DefaultInventoryCapacityModel.md) | 66 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultInventoryCapacityModel.CalculateInventoryCapacity](../source-paths/DefaultInventoryCapacityModel.md) | 68 | 100 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardMasterOfPlanning

Choice leaf: `perk.StewardMasterOfPlanning.choice`. Required skill: 250; alternative: `StewardMasterOfWarcraft`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2280`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardMasterOfPlanning.primary | {VALUE}% food consumption while your party is in a siege camp. | Quartermaster | -0.4f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardMasterOfPlanning.secondary | {VALUE}% effectiveness to continuous projects in the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingEffectModel.GetBuildingEffect](../source-paths/DefaultBuildingEffectModel.md) | 31 | 38 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 82 | 87 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 84 | 87 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardMasterOfWarcraft

Choice leaf: `perk.StewardMasterOfWarcraft.choice`. Required skill: 250; alternative: `StewardMasterOfPlanning`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2281`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardMasterOfWarcraft.primary | {VALUE}% troop wages while your party is in a siege camp. | Quartermaster | -0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardMasterOfWarcraft.secondary | {VALUE}% food consumption of town population in the governed settlement. | Governor | -0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 191 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 193 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementFoodModel.CalculateTownFoodChangeInternal](../source-paths/DefaultSettlementFoodModel.md) | 54 | 97 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## StewardPriceOfLoyalty

Choice leaf: `perk.StewardPriceOfLoyalty.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2282`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.StewardPriceOfLoyalty.primary | {VALUE}% to food consumption, wages and combat related morale loss for each steward point above 250 in your party. | Quartermaster | -0.005f | AddFactor | TroopUsageFlags.Undefined |
| perk.StewardPriceOfLoyalty.secondary | {VALUE}% tax income for each skill point above 200 in the governed settlement | Governor | 0.005f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 197 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyFoodConsumptionModel.CalculatePerkEffects](../source-paths/DefaultMobilePartyFoodConsumptionModel.md) | 61 | 87 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel.CalculateDailyTaxInternal](../source-paths/DefaultSettlementTaxModel.md) | 93 | 112 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementTaxModel.CalculateDailyTaxInternal](../source-paths/DefaultSettlementTaxModel.md) | 96 | 112 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
