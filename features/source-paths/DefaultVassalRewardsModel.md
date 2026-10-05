# DefaultVassalRewardsModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultVassalRewardsModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultVassalRewardsModel.GetEquipmentRewardsForJoiningKingdom.17.member | clans.005 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return itemRoster; | Locally assigned targets: itemRoster, randomBannerAtLevel | Calls in this member: ItemRoster, foreach, itemRoster.AddToCounts, GetRandomBannerAtLevel | TaleWorlds.CampaignSystem.GameComponents.DefaultVassalRewardsModel@17-30 |
| DefaultVassalRewardsModel.GetEquipmentRewardsForJoiningKingdom.17.guard-25 | clans.005 | Source predicate is true: randomBannerAtLevel != null | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: itemRoster.AddToCounts | TaleWorlds.CampaignSystem.GameComponents.DefaultVassalRewardsModel@17-30 |
| DefaultVassalRewardsModel.GetRandomBannerAtLevel.32.member | clans.005 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return e.GetRandomElementWithPredicate((ItemObject i) =&gt; (i.ItemComponent as BannerComponent).BannerLevel == bannerLevel);; return e.GetRandomElementWithPredicate((ItemObject i) =&gt; (i.ItemComponent as BannerComponent).BannerLevel == bannerLevel &amp;&amp; i.Culture == culture); | Locally assigned targets: e | Calls in this member: Campaign.Current.Models.BannerItemModel.GetPossibleRewardBannerItems, ToMBList, e.GetRandomElementWithPredicate | TaleWorlds.CampaignSystem.GameComponents.DefaultVassalRewardsModel@32-40 |
| DefaultVassalRewardsModel.GetRandomBannerAtLevel.32.guard-35 | clans.005 | Source predicate is true: culture == null | Local true-branch returns: return e.GetRandomElementWithPredicate((ItemObject i) =&gt; (i.ItemComponent as BannerComponent).BannerLevel == bannerLevel); | True-branch assignment targets: none located | True-branch calls: e.GetRandomElementWithPredicate | TaleWorlds.CampaignSystem.GameComponents.DefaultVassalRewardsModel@32-40 |
| DefaultVassalRewardsModel.GetTroopRewardsForJoiningKingdom.42.member | clans.005 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return troopRoster; | Locally assigned targets: troopRoster | Calls in this member: TroopRoster.CreateDummyTroopRoster, foreach, troopRoster.AddToCounts | TaleWorlds.CampaignSystem.GameComponents.DefaultVassalRewardsModel@42-50 |
