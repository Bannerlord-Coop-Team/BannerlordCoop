# DefaultBuildingEffectModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingEffectModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultBuildingEffectModel.GetBuildingEffect.11.member | buildings.007 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return bonuses; | Locally assigned targets: baseBuildingEffectAmount, bonuses, num | Calls in this member: building.BuildingType.GetBaseBuildingEffectAmount, ExplainedNumber, foreach, PerkHelper.AddPerkBonusForTown | TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingEffectModel@11-38 |
| DefaultBuildingEffectModel.GetBuildingEffect.11.guard-15 | buildings.007 | Source predicate is true: effect == BuildingEffectEnum.DenarByBoundVillageHeartPerDay | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num, bonuses | True-branch calls: foreach, ExplainedNumber | TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingEffectModel@11-38 |
| DefaultBuildingEffectModel.GetBuildingEffect.11.guard-24 | buildings.007 | Source predicate is true: effect == BuildingEffectEnum.FoodStock &amp;&amp; (building.BuildingType == DefaultBuildingTypes.CastleGranary &#124;&#124; building.BuildingType == DefaultBuildingTypes.SettlementWarehouse) | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: PerkHelper.AddPerkBonusForTown | TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingEffectModel@11-38 |
| DefaultBuildingEffectModel.GetBuildingEffect.11.guard-29 | buildings.007 | Source predicate is true: building.BuildingType.IsDailyProject | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: PerkHelper.AddPerkBonusForTown | TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingEffectModel@11-38 |
| DefaultBuildingEffectModel.GetBuildingEffect.11.guard-33 | buildings.007 | Source predicate is true: building.BuildingType == DefaultBuildingTypes.SettlementMarketplace &#124;&#124; building.BuildingType == DefaultBuildingTypes.SettlementDailyFestivalAndGames | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: PerkHelper.AddPerkBonusForTown | TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingEffectModel@11-38 |
