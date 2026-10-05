# DefaultRaidModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultRaidModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultRaidModel.CalculateHitDamage.46.member | villages.004 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return result; | Locally assigned targets: num, result | Calls in this member: MathF.Sqrt, ExplainedNumber, foreach, party.Party.MobileParty.LeaderHero.GetPerkValue, result.AddFactor | TaleWorlds.CampaignSystem.GameComponents.DefaultRaidModel@46-58 |
| DefaultRaidModel.CalculateHitDamage.46.guard-52 | villages.004 | Source predicate is true: party.Party.MobileParty?.LeaderHero != null &amp;&amp; party.Party.MobileParty.LeaderHero.GetPerkValue(DefaultPerks.Roguery.NoRestForTheWicked) | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: result.AddFactor | TaleWorlds.CampaignSystem.GameComponents.DefaultRaidModel@46-58 |
| DefaultRaidModel.GetRaidLootMultiplier.60.member | villages.004 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return new ExplainedNumber(1f); | Locally assigned targets: none located | Calls in this member: ExplainedNumber | TaleWorlds.CampaignSystem.GameComponents.DefaultRaidModel@60-63 |
| DefaultRaidModel.GetCommonLootItemScores.65.member | villages.004 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return CommonLootItemSpawnChances; | Locally assigned targets: none located | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultRaidModel@65-68 |
