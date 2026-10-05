# DefaultPregnancyModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultPregnancyModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultPregnancyModel.IsHeroAgeSuitableForPregnancy.23.member | heroes.011 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return hero.Age &lt;= 45f;; return false; | Locally assigned targets: none located | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultPregnancyModel@23-30 |
| DefaultPregnancyModel.IsHeroAgeSuitableForPregnancy.23.guard-25 | heroes.011 | Source predicate is true: hero.Age &gt;= 18f | Local true-branch returns: return hero.Age &lt;= 45f; | True-branch assignment targets: none located | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultPregnancyModel@23-30 |
| DefaultPregnancyModel.GetDailyChanceOfPregnancyForHero.32.member | heroes.011 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return explainedNumber.ResultNumber; | Locally assigned targets: num, num2, count, num3, num4, baseNumber, explainedNumber | Calls in this member: Math.Min, IsHeroAgeSuitableForPregnancy, ExplainedNumber, hero.GetPerkValue, hero.Spouse.GetPerkValue, explainedNumber.AddFactor | TaleWorlds.CampaignSystem.GameComponents.DefaultPregnancyModel@32-46 |
| DefaultPregnancyModel.GetDailyChanceOfPregnancyForHero.32.guard-41 | heroes.011 | Source predicate is true: hero.GetPerkValue(DefaultPerks.Charm.Virile) &#124;&#124; hero.Spouse.GetPerkValue(DefaultPerks.Charm.Virile) | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: none located | True-branch calls: explainedNumber.AddFactor | TaleWorlds.CampaignSystem.GameComponents.DefaultPregnancyModel@32-46 |
