# DefaultGenericXpModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultGenericXpModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultGenericXpModel.GetXpMultiplier.8.member | progression.001 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return 1.2f;; return 1f; | Locally assigned targets: none located | Calls in this member: Hero.MainHero.GetPerkValue | TaleWorlds.CampaignSystem.GameComponents.DefaultGenericXpModel@8-15 |
| DefaultGenericXpModel.GetXpMultiplier.8.guard-10 | progression.001 | Source predicate is true: hero.IsPlayerCompanion &amp;&amp; Hero.MainHero.GetPerkValue(DefaultPerks.Charm.NaturalLeader) | Local true-branch returns: return 1.2f; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultGenericXpModel@8-15 |
