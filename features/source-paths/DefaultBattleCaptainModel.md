# DefaultBattleCaptainModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultBattleCaptainModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultBattleCaptainModel.GetCaptainRatingForTroopUsages.11.member | battles.017 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return num / 1650f; | Locally assigned targets: num, compatiblePerks | Calls in this member: foreach, PerkHelper.GetCaptainPerksForTroopUsages, hero.GetPerkValue, compatiblePerks.Add | TaleWorlds.CampaignSystem.GameComponents.DefaultBattleCaptainModel@11-24 |
| DefaultBattleCaptainModel.GetCaptainRatingForTroopUsages.11.guard-17 | battles.017 | Source predicate is true: hero.GetPerkValue(captainPerksForTroopUsage) | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num | True-branch calls: compatiblePerks.Add | TaleWorlds.CampaignSystem.GameComponents.DefaultBattleCaptainModel@11-24 |
