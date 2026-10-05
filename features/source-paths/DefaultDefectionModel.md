# DefaultDefectionModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultDefectionModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultDefectionModel.CanHeroDefectToFaction.7.member | clans.009 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return !hero.IsPrisoner;; return false; | Locally assigned targets: none located | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultDefectionModel@7-14 |
| DefaultDefectionModel.CanHeroDefectToFaction.7.guard-9 | clans.009 | Source predicate is true: hero != null &amp;&amp; hero.MapFaction != null &amp;&amp; hero.MapFaction.IsKingdomFaction &amp;&amp; hero.MapFaction != Hero.MainHero.MapFaction &amp;&amp; hero.MapFaction.Leader != hero &amp;&amp; hero.Clan != null &amp;&amp; !hero.Clan.IsMinorFaction &amp;&amp; !hero.Clan.IsUnderMercenaryService &amp;&amp; hero.Clan.Kingdom != null | Local true-branch returns: return !hero.IsPrisoner; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultDefectionModel@7-14 |
