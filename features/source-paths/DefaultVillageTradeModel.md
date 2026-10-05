# DefaultVillageTradeModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultVillageTradeModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultVillageTradeModel.TradeBoundDistanceLimitAsDays.10.member | villages.003 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(navigationType) * 3f / (Campaign.Current.EstimatedAverageVillagerPartySpeed * (float)CampaignTime.HoursInDay); | Locally assigned targets: none located | Calls in this member: Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType | TaleWorlds.CampaignSystem.GameComponents.DefaultVillageTradeModel@10-13 |
| DefaultVillageTradeModel.GetTradeBoundToAssignForVillage.15.member | villages.003 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return settlement;; return settlement2;; return null; | Locally assigned targets: navigationType, settlement, distanceLimit, settlement2 | Calls in this member: SettlementHelper.FindNearestSettlementToSettlement, Campaign.Current.Models.VillageTradeModel.TradeBoundDistanceLimitAsDays, Campaign.Current.Models.MapDistanceModel.GetDistance, x.Town.MapFaction.IsAtWarWith | TaleWorlds.CampaignSystem.GameComponents.DefaultVillageTradeModel@15-30 |
| DefaultVillageTradeModel.GetTradeBoundToAssignForVillage.15.guard-20 | villages.003 | Source predicate is true: settlement != null &amp;&amp; Campaign.Current.Models.MapDistanceModel.GetDistance(settlement, village.Settlement, isFromPort: false, isTargetingPort: false, navigationType) &lt; distanceLimit | Local true-branch returns: return settlement; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultVillageTradeModel@15-30 |
| DefaultVillageTradeModel.GetTradeBoundToAssignForVillage.15.guard-25 | villages.003 | Source predicate is true: settlement2 != null &amp;&amp; Campaign.Current.Models.MapDistanceModel.GetDistance(settlement2, village.Settlement, isFromPort: false, isTargetingPort: false, navigationType) &lt; distanceLimit | Local true-branch returns: return settlement2; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultVillageTradeModel@15-30 |
