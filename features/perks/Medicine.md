# Medicine perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## MedicineSelfMedication

Choice leaf: `perk.MedicineSelfMedication.choice`. Required skill: 25; alternative: `MedicinePreventiveMedicine`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2283`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineSelfMedication.primary | {VALUE}% healing rate. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicineSelfMedication.secondary | {VALUE}% combat movement speed. | Personal | 0.02f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetHeroesEffectedHealingAmount](../source-paths/DefaultPartyHealingModel.md) | 293 | 300 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.SetPerkAndBannerEffectsOnAgent](../source-paths/SandboxAgentStatCalculateModel.md) | 1500 | 1586 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicinePreventiveMedicine

Choice leaf: `perk.MedicinePreventiveMedicine.choice`. Required skill: 25; alternative: `MedicineSelfMedication`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2284`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicinePreventiveMedicine.primary | {VALUE} hit points. | Personal | 5f | Add | TroopUsageFlags.Undefined |
| perk.MedicinePreventiveMedicine.secondary | {VALUE}% recovery of lost hit points after each battle. | Personal | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetBattleEndHealingAmount](../source-paths/DefaultPartyHealingModel.md) | 305 | 314 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetBattleEndHealingAmount](../source-paths/DefaultPartyHealingModel.md) | 307 | 314 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel.MaxHitpoints](../source-paths/DefaultCharacterStatsModel.md) | 36 | 47 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineTriageTent

Choice leaf: `perk.MedicineTriageTent.choice`. Required skill: 50; alternative: `MedicineWalkItOff`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2285`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineTriageTent.primary | {VALUE}% healing rate when stationary on the campaign map. | Surgeon | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicineTriageTent.secondary | {VALUE}% food consumption for besieged governed settlement. | Governor | -0.05f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 166 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingHpForHeroes](../source-paths/DefaultPartyHealingModel.md) | 255 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementFoodModel.CalculateTownFoodChangeInternal](../source-paths/DefaultSettlementFoodModel.md) | 52 | 97 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineWalkItOff

Choice leaf: `perk.MedicineWalkItOff.choice`. Required skill: 50; alternative: `MedicineTriageTent`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2286`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineWalkItOff.primary | {VALUE}% healing rate when moving on the campaign map. | Surgeon | 0.15f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicineWalkItOff.secondary | {VALUE} hit points recovery after each offensive battle. | Personal | 10f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 170 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingHpForHeroes](../source-paths/DefaultPartyHealingModel.md) | 259 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetBattleEndHealingAmount](../source-paths/DefaultPartyHealingModel.md) | 309 | 314 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetBattleEndHealingAmount](../source-paths/DefaultPartyHealingModel.md) | 311 | 314 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineSledges

Choice leaf: `perk.MedicineSledges.choice`. Required skill: 75; alternative: `MedicineDoctorsOath`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2287`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineSledges.primary | {VALUE}% party speed penalty from the wounded. | Surgeon | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicineSledges.secondary | {VALUE} hit points to mounts in your party. | PartyLeader | 15f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartySpeedCalculatingModel.GetWoundedModifier](../source-paths/DefaultPartySpeedCalculatingModel.md) | 406 | 409 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveMaxHealth](../source-paths/SandboxAgentStatCalculateModel.md) | 776 | 782 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineDoctorsOath

Choice leaf: `perk.MedicineDoctorsOath.choice`. Required skill: 75; alternative: `MedicineSledges`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2288`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineDoctorsOath.primary | Your medicine skill partially applies to enemy casualties, increasing potential prisoners. | Surgeon | 0f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicineDoctorsOath.secondary | {VALUE} hit points. | Personal | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetSurvivalChance](../source-paths/DefaultPartyHealingModel.md) | 72 | 115 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel.MaxHitpoints](../source-paths/DefaultCharacterStatsModel.md) | 32 | 47 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineBestMedicine

Choice leaf: `perk.MedicineBestMedicine.choice`. Required skill: 100; alternative: `MedicineGoodLodging`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2289`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineBestMedicine.primary | {VALUE}% healing rate while party morale is above 70. | Surgeon | 0.15f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicineBestMedicine.secondary | {VALUE} relationship per day with a random notable over age 40 when party is in a town. | Personal | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 176 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingHpForHeroes](../source-paths/DefaultPartyHealingModel.md) | 265 | 287 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineGoodLodging

Choice leaf: `perk.MedicineGoodLodging.choice`. Required skill: 100; alternative: `MedicineBestMedicine`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2290`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineGoodLodging.primary | {VALUE}% healing rate while resting in settlements. | Surgeon | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicineGoodLodging.secondary | {VALUE} relationship per day with a random noble over age 40 when party is in a town. | Personal | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 191 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingHpForHeroes](../source-paths/DefaultPartyHealingModel.md) | 278 | 287 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineSiegeMedic

Choice leaf: `perk.MedicineSiegeMedic.choice`. Required skill: 125; alternative: `MedicineVeterinarian`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2291`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineSiegeMedic.primary | {VALUE}% chance of troops getting wounded instead of getting killed during siege bombardment. | Surgeon | 0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicineSiegeMedic.secondary | {VALUE}% chance to recover from lethal wounds during siege bombardment. | Surgeon | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetSiegeBombardmentHitSurgeryChance](../source-paths/DefaultPartyHealingModel.md) | 54 | 59 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetSiegeBombardmentHitSurgeryChance](../source-paths/DefaultPartyHealingModel.md) | 56 | 59 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetCasualtyChance](../source-paths/DefaultSiegeEventModel.md) | 239 | 248 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetCasualtyChance](../source-paths/DefaultSiegeEventModel.md) | 241 | 248 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineVeterinarian

Choice leaf: `perk.MedicineVeterinarian.choice`. Required skill: 125; alternative: `MedicineSiegeMedic`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2292`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineVeterinarian.primary | {VALUE}% daily chance to recover a lame horse. | Surgeon | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicineVeterinarian.secondary | {VALUE}% chance to recover mounts of dead cavalry troops in battles. | Surgeon | 0.5f | AddFactor | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicinePristineStreets

Choice leaf: `perk.MedicinePristineStreets.choice`. Required skill: 150; alternative: `MedicineBushDoctor`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2293`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicinePristineStreets.primary | {VALUE} settlement prosperity every day in governed settlements. | Governor | 1f | Add | TroopUsageFlags.Undefined |
| perk.MedicinePristineStreets.secondary | {VALUE}% party healing rate while waiting in towns. | Surgeon | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 188 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingHpForHeroes](../source-paths/DefaultPartyHealingModel.md) | 275 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementProsperityModel.CalculateProsperityChangeInternal](../source-paths/DefaultSettlementProsperityModel.md) | 146 | 200 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineBushDoctor

Choice leaf: `perk.MedicineBushDoctor.choice`. Required skill: 150; alternative: `MedicinePristineStreets`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2294`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineBushDoctor.primary | {VALUE}% hearth growth in villages bound to the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicineBushDoctor.secondary | {VALUE}% party healing rate while waiting in villages. | Surgeon | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 196 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingHpForHeroes](../source-paths/DefaultPartyHealingModel.md) | 282 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementProsperityModel.CalculateHearthChangeInternal](../source-paths/DefaultSettlementProsperityModel.md) | 57 | 70 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicinePerfectHealth

Choice leaf: `perk.MedicinePerfectHealth.choice`. Required skill: 175; alternative: `MedicineHealthAdvise`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2295`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicinePerfectHealth.primary | {VALUE}% recovery rate for each type of food in party inventory. | Surgeon | 0.05f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicinePerfectHealth.secondary | {VALUE}% animal production rate in villages bound to the governed settlement. | Governor | 0.1f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 202 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 204 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 209 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVillageProductionCalculatorModel.CalculateDailyProductionAmount](../source-paths/DefaultVillageProductionCalculatorModel.md) | 51 | 74 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineHealthAdvise

Choice leaf: `perk.MedicineHealthAdvise.choice`. Required skill: 175; alternative: `MedicinePerfectHealth`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2296`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineHealthAdvise.primary | Chance of recovery from death due to old age for every clan member. | ClanLeader | 0f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicineHealthAdvise.secondary | Wounded troops do not decrease morale in battles. | Surgeon | 0f | AddFactor | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicinePhysicianOfPeople

Choice leaf: `perk.MedicinePhysicianOfPeople.choice`. Required skill: 200; alternative: `MedicineCleanInfrastructure`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2297`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicinePhysicianOfPeople.primary | {VALUE} loyalty per day in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |
| perk.MedicinePhysicianOfPeople.secondary | {VALUE}% chance to recover from lethal wounds for tier 1 and 2 troops | Surgeon | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetSurvivalChance](../source-paths/DefaultPartyHealingModel.md) | 80 | 115 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementLoyaltyModel.GetSettlementLoyaltyChangeDueToGovernorPerks](../source-paths/DefaultSettlementLoyaltyModel.md) | 128 | 167 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineCleanInfrastructure

Choice leaf: `perk.MedicineCleanInfrastructure.choice`. Required skill: 200; alternative: `MedicinePhysicianOfPeople`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2298`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineCleanInfrastructure.primary | {VALUE} prosperity bonus from civilian projects in the governed settlement. | Governor | 1f | Add | TroopUsageFlags.Undefined |
| perk.MedicineCleanInfrastructure.secondary | {VALUE}% recovery rate from raids in villages bound to the governed settlement. | Governor | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementProsperityModel.CalculateProsperityChangeInternal](../source-paths/DefaultSettlementProsperityModel.md) | 165 | 200 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineCheatDeath

Choice leaf: `perk.MedicineCheatDeath.choice`. Required skill: 225; alternative: `MedicineFortitudeTonic`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2299`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineCheatDeath.primary | Cheat death due to old age once. | Personal | 0f | Add | TroopUsageFlags.Undefined |
| perk.MedicineCheatDeath.secondary | {VALUE}% chance to die when you fall unconscious in battle. | Surgeon | -0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetSurvivalChance](../source-paths/DefaultPartyHealingModel.md) | 91 | 115 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetSurvivalChance](../source-paths/DefaultPartyHealingModel.md) | 93 | 115 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineFortitudeTonic

Choice leaf: `perk.MedicineFortitudeTonic.choice`. Required skill: 225; alternative: `MedicineCheatDeath`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2300`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineFortitudeTonic.primary | {VALUE} hit points to other heroes in your party. | PartyLeader | 10f | Add | TroopUsageFlags.Undefined |
| perk.MedicineFortitudeTonic.secondary | {VALUE} hit points. | Personal | 5f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel.MaxHitpoints](../source-paths/DefaultCharacterStatsModel.md) | 33 | 47 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel.MaxHitpoints](../source-paths/DefaultCharacterStatsModel.md) | 37 | 47 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel.MaxHitpoints](../source-paths/DefaultCharacterStatsModel.md) | 39 | 47 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineHelpingHands

Choice leaf: `perk.MedicineHelpingHands.choice`. Required skill: 250; alternative: `MedicineBattleHardened`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2301`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineHelpingHands.primary | {VALUE}% troop recovery rate for every 10 troop in your party. | Surgeon | 0.02f | AddFactor | TroopUsageFlags.Undefined |
| perk.MedicineHelpingHands.secondary | {VALUE}% prosperity loss from starvation. | Governor | -0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 211 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 214 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyHealingModel.GetDailyHealingForRegulars](../source-paths/DefaultPartyHealingModel.md) | 220 | 230 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementProsperityModel.CalculateProsperityChangeInternal](../source-paths/DefaultSettlementProsperityModel.md) | 78 | 200 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineBattleHardened

Choice leaf: `perk.MedicineBattleHardened.choice`. Required skill: 250; alternative: `MedicineHelpingHands`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2302`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineBattleHardened.primary | {VALUE} experience to wounded units at the end of the battle. | Surgeon | 25f | Add | TroopUsageFlags.Undefined |
| perk.MedicineBattleHardened.secondary | {VALUE}% siege attrition loss in the governed settlement. | Governor | -0.25f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetCasualtyChance](../source-paths/DefaultSiegeEventModel.md) | 243 | 248 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel.GetCasualtyChance](../source-paths/DefaultSiegeEventModel.md) | 245 | 248 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## MedicineMinisterOfHealth

Choice leaf: `perk.MedicineMinisterOfHealth.choice`. Required skill: 275; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2303`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.MedicineMinisterOfHealth.primary | {VALUE} hit point to troops for every skill point above 250. | Personal | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveMaxHealth](../source-paths/SandboxAgentStatCalculateModel.md) | 747 | 782 |
| [SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveMaxHealth](../source-paths/SandboxAgentStatCalculateModel.md) | 749 | 782 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
