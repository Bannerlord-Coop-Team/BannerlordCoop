# DefaultCompanionHiringPriceCalculationModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultCompanionHiringPriceCalculationModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultCompanionHiringPriceCalculationModel.GetCompanionHiringPrice.12.member | heroes.003 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return (int)stat.ResultNumber; | Locally assigned targets: stat, town, num, equipmentIndex, itemRosterElement, equipmentIndex2, itemRosterElement2 | Calls in this member: ExplainedNumber, SettlementHelper.FindNearestTownToMobileParty, town.GetItemPrice, stat.Add, Hero.MainHero.GetPerkValue, stat.AddFactor, PerkHelper.AddPerkBonusForParty, return | TaleWorlds.CampaignSystem.GameComponents.DefaultCompanionHiringPriceCalculationModel@12-48 |
| DefaultCompanionHiringPriceCalculationModel.GetCompanionHiringPrice.12.guard-16 | heroes.003 | Source predicate is true: town == null | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: town | True-branch calls: SettlementHelper.FindNearestTownToMobileParty | TaleWorlds.CampaignSystem.GameComponents.DefaultCompanionHiringPriceCalculationModel@12-48 |
| DefaultCompanionHiringPriceCalculationModel.GetCompanionHiringPrice.12.guard-24 | heroes.003 | Source predicate is true: itemRosterElement.Item != null | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num | True-branch calls: town.GetItemPrice | TaleWorlds.CampaignSystem.GameComponents.DefaultCompanionHiringPriceCalculationModel@12-48 |
| DefaultCompanionHiringPriceCalculationModel.GetCompanionHiringPrice.12.guard-32 | heroes.003 | Source predicate is true: itemRosterElement2.Item != null | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num | True-branch calls: town.GetItemPrice | TaleWorlds.CampaignSystem.GameComponents.DefaultCompanionHiringPriceCalculationModel@12-48 |
| DefaultCompanionHiringPriceCalculationModel.GetCompanionHiringPrice.12.guard-39 | heroes.003 | Source predicate is true: Hero.MainHero.IsPartyLeader &amp;&amp; Hero.MainHero.GetPerkValue(DefaultPerks.Steward.PaidInPromise) | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: stat.AddFactor | TaleWorlds.CampaignSystem.GameComponents.DefaultCompanionHiringPriceCalculationModel@12-48 |
| DefaultCompanionHiringPriceCalculationModel.GetCompanionHiringPrice.12.guard-43 | heroes.003 | Source predicate is true: Hero.MainHero.PartyBelongedTo != null | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: PerkHelper.AddPerkBonusForParty | TaleWorlds.CampaignSystem.GameComponents.DefaultCompanionHiringPriceCalculationModel@12-48 |
