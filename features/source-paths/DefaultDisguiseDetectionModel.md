# DefaultDisguiseDetectionModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultDisguiseDetectionModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultDisguiseDetectionModel.CalculateDisguiseDetectionProbability.12.member | locations.002 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return MathF.Clamp(num3, 0f, 1f); | Locally assigned targets: num, num2, num3 | Calls in this member: foreach, settlement.Town.GarrisonParty.MemberRoster.GetTroopRoster, MathF.Max, Hero.MainHero.GetSkillValue, Hero.MainHero.CharacterObject.GetPerkValue, MathF.Clamp | TaleWorlds.CampaignSystem.GameComponents.DefaultDisguiseDetectionModel@12-31 |
| DefaultDisguiseDetectionModel.CalculateDisguiseDetectionProbability.12.guard-16 | locations.002 | Source predicate is true: settlement.Town != null &amp;&amp; settlement.Town.GarrisonParty != null | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num2, num | True-branch calls: foreach, settlement.Town.GarrisonParty.MemberRoster.GetTroopRoster | TaleWorlds.CampaignSystem.GameComponents.DefaultDisguiseDetectionModel@12-31 |
| DefaultDisguiseDetectionModel.CalculateDisguiseDetectionProbability.12.guard-26 | locations.002 | Source predicate is true: Hero.MainHero.CharacterObject.GetPerkValue(DefaultPerks.Roguery.TwoFaced) &amp;&amp; num3 &gt; 0f | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num3 | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultDisguiseDetectionModel@12-31 |
