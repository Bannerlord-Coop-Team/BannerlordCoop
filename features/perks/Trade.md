# Trade perks

[Index](../perks.md) · [Source identities](../baseline/behavior-sources.json)

All entries are definition-inspected; runtime is unrun. The definitions, selection and effects
are separate leaves. Located consumer mentions still need full caller/role/context interpretation.

## TradeAppraiser

Choice leaf: `perk.TradeAppraiser.choice`. Required skill: 25; alternative: `TradeWholeSeller`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2239`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeAppraiser.primary | {VALUE}% price penalty while selling equipment. | PartyLeader | -0.15f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeAppraiser.secondary | Your profits are marked. | Personal | 0f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 104 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 106 | 157 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeWholeSeller

Choice leaf: `perk.TradeWholeSeller.choice`. Required skill: 25; alternative: `TradeAppraiser`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2240`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeWholeSeller.primary | {VALUE}% price penalty while selling trade goods. | PartyLeader | -0.15f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeWholeSeller.secondary | Your profits are marked. | Personal | 0f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 95 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 97 | 157 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeCaravanMaster

Choice leaf: `perk.TradeCaravanMaster.choice`. Required skill: 50; alternative: `TradeMarketDealer`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2241`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeCaravanMaster.primary | {VALUE}% carrying capacity for your party. | Quartermaster | 0.3f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeCaravanMaster.secondary | Item prices are marked relative to the average price. | Personal | 0f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultInventoryCapacityModel.CalculateInventoryCapacity](../source-paths/DefaultInventoryCapacityModel.md) | 93 | 100 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultInventoryCapacityModel.CalculateInventoryCapacity](../source-paths/DefaultInventoryCapacityModel.md) | 95 | 100 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeMarketDealer

Choice leaf: `perk.TradeMarketDealer.choice`. Required skill: 50; alternative: `TradeCaravanMaster`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2242`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeMarketDealer.primary | {VALUE}% cost of bartering for safe passage. | ClanLeader | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeMarketDealer.secondary | Item prices are marked relative to the average price. | Personal | 0f | AddFactor | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeDistributedGoods

Choice leaf: `perk.TradeDistributedGoods.choice`. Required skill: 75; alternative: `TradeLocalConnection`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2243`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeDistributedGoods.primary | Double the relationship gain by resolved issues with artisans. | Personal | 2f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeDistributedGoods.secondary | {VALUE}% price penalty while buying from villages. | Quartermaster | -0.15f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 139 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 141 | 157 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeLocalConnection

Choice leaf: `perk.TradeLocalConnection.choice`. Required skill: 75; alternative: `TradeDistributedGoods`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2244`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeLocalConnection.primary | Double the relationship gain by resolved issues with merchants. | Personal | 2f | Add | TroopUsageFlags.Undefined |
| perk.TradeLocalConnection.secondary | {VALUE}% price penalty while selling animals. | Quartermaster | -0.15f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 143 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 145 | 157 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeTravelingRumors

Choice leaf: `perk.TradeTravelingRumors.choice`. Required skill: 100; alternative: `TradeTollgates`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2245`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeTravelingRumors.primary | Your caravans gather trade rumors. | Personal | 0f | Add | TroopUsageFlags.Undefined |
| perk.TradeTravelingRumors.secondary | {VALUE} gold for each villager party visiting the governed settlement. | Governor | 20f | Add | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeTollgates

Choice leaf: `perk.TradeTollgates.choice`. Required skill: 100; alternative: `TradeTravelingRumors`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2246`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeTollgates.primary | Your workshops gather trade rumors. | Personal | 0f | Add | TroopUsageFlags.Undefined |
| perk.TradeTollgates.secondary | {VALUE} gold for each caravan visiting the governed settlement. | Governor | 30f | Add | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeArtisanCommunity

Choice leaf: `perk.TradeArtisanCommunity.choice`. Required skill: 125; alternative: `TradeGreatInvestor`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2247`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeArtisanCommunity.primary | {VALUE} daily renown from every profiting workshop. | ClanLeader | 1f | Add | TroopUsageFlags.Undefined |
| perk.TradeArtisanCommunity.secondary | {VALUE} recruitment slot when recruiting from merchant notables. | Quartermaster | 1f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateHeroIncomeFromWorkshops](../source-paths/DefaultClanFinanceModel.md) | 930 | 934 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateHeroIncomeFromWorkshops](../source-paths/DefaultClanFinanceModel.md) | 932 | 934 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero](../source-paths/DefaultVolunteerModel.md) | 26 | 47 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero](../source-paths/DefaultVolunteerModel.md) | 28 | 47 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeGreatInvestor

Choice leaf: `perk.TradeGreatInvestor.choice`. Required skill: 125; alternative: `TradeArtisanCommunity`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2248`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeGreatInvestor.primary | {VALUE} daily renown from every profiting caravan. | ClanLeader | 1f | Add | TroopUsageFlags.Undefined |
| perk.TradeGreatInvestor.secondary | {VALUE}% companion recruitment cost. | Quartermaster | -0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.AddIncomeFromParty](../source-paths/DefaultClanFinanceModel.md) | 706 | 718 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.AddIncomeFromParty](../source-paths/DefaultClanFinanceModel.md) | 708 | 718 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultCompanionHiringPriceCalculationModel.GetCompanionHiringPrice](../source-paths/DefaultCompanionHiringPriceCalculationModel.md) | 45 | 48 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeMercenaryConnections

Choice leaf: `perk.TradeMercenaryConnections.choice`. Required skill: 150; alternative: `TradeContentTrades`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2249`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeMercenaryConnections.primary | {VALUE}% workshop production rate. | Governor | 0.25f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeMercenaryConnections.secondary | {VALUE}% mercenary troop wages in your party. | PartyLeader | -0.25f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 168 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 173 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 174 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultWorkshopModel.GetEffectiveConversionSpeedOfProduction](../source-paths/DefaultWorkshopModel.md) | 50 | 53 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeContentTrades

Choice leaf: `perk.TradeContentTrades.choice`. Required skill: 150; alternative: `TradeMercenaryConnections`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2250`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeContentTrades.primary | {VALUE}% tariff income in the governed settlement. | Governor | 0.1f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeContentTrades.secondary | {VALUE}% wages paid while waiting in settlements. | PartyLeader | -0.5f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 199 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 201 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateTownIncomeFromTariffs](../source-paths/DefaultClanFinanceModel.md) | 438 | 452 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeInsurancePlans

Choice leaf: `perk.TradeInsurancePlans.choice`. Required skill: 175; alternative: `TradeRapidDevelopment`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2251`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeInsurancePlans.primary | {VALUE} denar return when one of your caravans is destroyed. | ClanLeader | 5000f | Add | TroopUsageFlags.Undefined |
| perk.TradeInsurancePlans.secondary | {VALUE}% price penalty while buying food items. | Quartermaster | -0.25f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 118 | 157 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeRapidDevelopment

Choice leaf: `perk.TradeRapidDevelopment.choice`. Required skill: 175; alternative: `TradeInsurancePlans`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2252`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeRapidDevelopment.primary | {VALUE} denar return for each workshop when workshop's town is captured by an enemy. | ClanLeader | 5000f | Add | TroopUsageFlags.Undefined |
| perk.TradeRapidDevelopment.secondary | {VALUE}% price penalty while buying clay, iron, silk and silver. | Quartermaster | -0.25f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 151 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 153 | 157 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeGranaryAccountant

Choice leaf: `perk.TradeGranaryAccountant.choice`. Required skill: 200; alternative: `TradeTradeyardForeman`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2253`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeGranaryAccountant.primary | {VALUE}% price penalty while selling food items. | Personal | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeGranaryAccountant.secondary | {VALUE}% production rate to grain, olives, fish, date in villages bound to the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 101 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVillageProductionCalculatorModel.CalculateDailyProductionAmount](../source-paths/DefaultVillageProductionCalculatorModel.md) | 39 | 74 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeTradeyardForeman

Choice leaf: `perk.TradeTradeyardForeman.choice`. Required skill: 200; alternative: `TradeGranaryAccountant`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2254`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeTradeyardForeman.primary | {VALUE}% price penalty while selling pottery, tools, silk and jewelry. | Personal | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeTradeyardForeman.secondary | {VALUE}% production rate to clay, iron, silk and silver in villages bound to the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetTradePenalty](../source-paths/DefaultTradeItemPriceFactorModel.md) | 149 | 157 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultVillageProductionCalculatorModel.CalculateDailyProductionAmount](../source-paths/DefaultVillageProductionCalculatorModel.md) | 43 | 74 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeSwordForBarter

Choice leaf: `perk.TradeSwordForBarter.choice`. Required skill: 225; alternative: `TradeSelfMadeMan`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2255`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeSwordForBarter.primary | {VALUE}% hiring costs of mercenary troops. | Personal | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeSwordForBarter.secondary | {VALUE}% caravan guard wages. | Quartermaster | -0.15f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 150 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 155 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTotalWage](../source-paths/DefaultPartyWageModel.md) | 156 | 204 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 275 | 287 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.GetTroopRecruitmentCost](../source-paths/DefaultPartyWageModel.md) | 277 | 287 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeSelfMadeMan

Choice leaf: `perk.TradeSelfMadeMan.choice`. Required skill: 225; alternative: `TradeSwordForBarter`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2256`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeSelfMadeMan.primary | {VALUE}% barter penalty for items. | Personal | -0.5f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeSelfMadeMan.secondary | {VALUE}% build speed for marketplace, kiln and aqueduct projects. | Governor | 0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.CalculateDailyConstructionPowerInternal](../source-paths/DefaultBuildingConstructionModel.md) | 137 | 160 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.GetBarterPenalty](../source-paths/DefaultBarterModel.md) | 64 | 97 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.GetBarterPenalty](../source-paths/DefaultBarterModel.md) | 66 | 97 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.GetBarterPenalty](../source-paths/DefaultBarterModel.md) | 86 | 97 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBarterModel.GetBarterPenalty](../source-paths/DefaultBarterModel.md) | 88 | 97 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeSilverTongue

Choice leaf: `perk.TradeSilverTongue.choice`. Required skill: 250; alternative: `TradeSpringOfGold`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2257`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeSilverTongue.primary | {VALUE}% gold required while persuading lords to defect to your faction. | Personal | -0.15f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeSilverTongue.secondary | {VALUE}% better trade deals from caravans and villagers | Quartermaster | 0.15f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetPrice](../source-paths/DefaultTradeItemPriceFactorModel.md) | 189 | 194 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultTradeItemPriceFactorModel.GetPrice](../source-paths/DefaultTradeItemPriceFactorModel.md) | 191 | 194 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeSpringOfGold

Choice leaf: `perk.TradeSpringOfGold.choice`. Required skill: 250; alternative: `TradeSilverTongue`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2258`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeSpringOfGold.primary | {VALUE}% denars of interest income per day based on your current denars up to 1000 denars. | ClanLeader | 0.001f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeSpringOfGold.secondary | {VALUE}% effect from boosting projects in the governed settlement. | Governor | 0.2f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateClanIncomeInternal](../source-paths/DefaultClanFinanceModel.md) | 160 | 165 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateClanIncomeInternal](../source-paths/DefaultClanFinanceModel.md) | 162 | 165 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel.CalculateClanIncomeInternal](../source-paths/DefaultClanFinanceModel.md) | 163 | 165 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.GetBoostAmount](../source-paths/DefaultBuildingConstructionModel.md) | 66 | 71 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingConstructionModel.GetBoostAmount](../source-paths/DefaultBuildingConstructionModel.md) | 68 | 71 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeManOfMeans

Choice leaf: `perk.TradeManOfMeans.choice`. Required skill: 275; alternative: `TradeTrickleDown`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2259`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeManOfMeans.primary | {VALUE}% costs of recruiting minor faction clans into your clan. | ClanLeader | -0.2f | AddFactor | TroopUsageFlags.Undefined |
| perk.TradeManOfMeans.secondary | {VALUE}% ransom cost for your freedom. | Personal | -0.3f | AddFactor | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMinorFactionsModel.GetMercenaryAwardFactorToJoinKingdom](../source-paths/DefaultMinorFactionsModel.md) | 89 | 98 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultMinorFactionsModel.GetMercenaryAwardFactorToJoinKingdom](../source-paths/DefaultMinorFactionsModel.md) | 91 | 98 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeTrickleDown

Choice leaf: `perk.TradeTrickleDown.choice`. Required skill: 275; alternative: `TradeManOfMeans`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2260`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeTrickleDown.primary | {VALUE} relationship with merchants if 10.000 or more denars are spent on a single deal. | PartyLeader | 1f | Add | TroopUsageFlags.Undefined |
| perk.TradeTrickleDown.secondary | {VALUE} daily prosperity while building a project in the governed settlement. | Governor | 2f | Add | TroopUsageFlags.Undefined |

Located consumer mentions, shared as unresolved candidates for both effects:

| Containing member | Mention line | Complete member ends |
| --- | ---: | ---: |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementProsperityModel.CalculateProsperityChangeInternal](../source-paths/DefaultSettlementProsperityModel.md) | 176 | 200 |
| [TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementProsperityModel.CalculateProsperityChangeInternal](../source-paths/DefaultSettlementProsperityModel.md) | 178 | 200 |

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.

## TradeEverythingHasAPrice

Choice leaf: `perk.TradeEverythingHasAPrice.choice`. Required skill: 300; alternative: `null`.
Definition: `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks:2261`.

| Effect leaf | Advertised effect | Role | Raw bonus | Increment | Troop mask |
| --- | --- | --- | --- | --- | --- |
| perk.TradeEverythingHasAPrice.primary | You can now trade settlements in barter. | Personal | 0f | Invalid | TroopUsageFlags.Undefined |

No consumer mention located in the inspected campaign/agent model set. Behaviors, helpers and native paths remain unresolved.

Independent controls: nonholder, ineligible role/troop/context, and relevant cap or boundary.
Observe the exact affected state on the owner and permitted shared consequence on the other real client.
Persistence of the selection does not establish persistence or application of this effect.
