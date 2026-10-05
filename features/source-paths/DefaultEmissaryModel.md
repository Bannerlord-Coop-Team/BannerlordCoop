# DefaultEmissaryModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultEmissaryModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultEmissaryModel.IsEmissary.9.member | heroes.005 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return hero.Age &gt;= (float)Campaign.Current.Models.AgeModel.HeroComesOfAge;; return false; | Locally assigned targets: none located | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultEmissaryModel@9-16 |
| DefaultEmissaryModel.IsEmissary.9.guard-11 | heroes.005 | Source predicate is true: (hero.CompanionOf == Clan.PlayerClan &#124;&#124; hero.Clan == Clan.PlayerClan) &amp;&amp; hero.PartyBelongedTo == null &amp;&amp; hero.CurrentSettlement != null &amp;&amp; hero.CurrentSettlement.IsFortification &amp;&amp; !hero.IsPrisoner | Local true-branch returns: return hero.Age &gt;= (float)Campaign.Current.Models.AgeModel.HeroComesOfAge; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultEmissaryModel@9-16 |
