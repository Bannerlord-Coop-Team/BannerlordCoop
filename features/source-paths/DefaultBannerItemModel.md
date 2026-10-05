# DefaultBannerItemModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultBannerItemModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultBannerItemModel.GetPossibleRewardBannerItems.19.member | presentation.001 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return Items.All.WhereQ((ItemObject i) =&gt; i.IsBannerItem &amp;&amp; i.StringId != "campaign_banner_small"); | Locally assigned targets: none located | Calls in this member: Items.All.WhereQ | TaleWorlds.CampaignSystem.GameComponents.DefaultBannerItemModel@19-22 |
| DefaultBannerItemModel.GetPossibleRewardBannerItemsForHero.24.member | presentation.001 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return list; | Locally assigned targets: possibleRewardBannerItems, bannerItemLevelForHero, list | Calls in this member: GetPossibleRewardBannerItems, GetBannerItemLevelForHero, foreach, list.Add | TaleWorlds.CampaignSystem.GameComponents.DefaultBannerItemModel@24-37 |
| DefaultBannerItemModel.GetPossibleRewardBannerItemsForHero.24.guard-31 | presentation.001 | Source predicate is true: (item.Culture == null &#124;&#124; item.Culture == hero.Culture) &amp;&amp; (item.ItemComponent as BannerComponent).BannerLevel == bannerItemLevelForHero | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: list.Add | TaleWorlds.CampaignSystem.GameComponents.DefaultBannerItemModel@24-37 |
| DefaultBannerItemModel.GetBannerItemLevelForHero.39.member | presentation.001 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return 3;; return 2;; return 1; | Locally assigned targets: none located | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultBannerItemModel@39-50 |
| DefaultBannerItemModel.GetBannerItemLevelForHero.39.guard-41 | presentation.001 | Source predicate is true: hero.Clan != null &amp;&amp; hero.Clan.Leader == hero | Local true-branch returns: return 3;; return 2; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultBannerItemModel@39-50 |
| DefaultBannerItemModel.GetBannerItemLevelForHero.39.guard-43 | presentation.001 | Source predicate is true: hero.MapFaction.IsKingdomFaction &amp;&amp; hero.Clan.Kingdom.RulingClan == hero.Clan | Local true-branch returns: return 3; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultBannerItemModel@39-50 |
| DefaultBannerItemModel.CanBannerBeUpdated.52.member | presentation.001 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return true; | Locally assigned targets: none located | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultBannerItemModel@52-55 |
