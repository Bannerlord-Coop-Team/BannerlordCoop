# DefaultHeroDeathProbabilityCalculationModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultHeroDeathProbabilityCalculationModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultHeroDeathProbabilityCalculationModel.CalculateHeroDeathProbability.8.member | heroes.014 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return CalculateHeroDeathProbabilityInternal(hero); | Locally assigned targets: none located | Calls in this member: CalculateHeroDeathProbabilityInternal | TaleWorlds.CampaignSystem.GameComponents.DefaultHeroDeathProbabilityCalculationModel@8-11 |
| DefaultHeroDeathProbabilityCalculationModel.CalculateHeroDeathProbabilityInternal.13.member | heroes.014 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return num; | Locally assigned targets: num, becomeOldAge, num2, num3, num4 | Calls in this member: MathF.Pow | TaleWorlds.CampaignSystem.GameComponents.DefaultHeroDeathProbabilityCalculationModel@13-35 |
| DefaultHeroDeathProbabilityCalculationModel.CalculateHeroDeathProbabilityInternal.13.guard-16 | heroes.014 | Source predicate is true: !CampaignOptions.IsLifeDeathCycleDisabled | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: becomeOldAge, num2, num3, num4, num | True-branch calls: MathF.Pow | TaleWorlds.CampaignSystem.GameComponents.DefaultHeroDeathProbabilityCalculationModel@13-35 |
| DefaultHeroDeathProbabilityCalculationModel.CalculateHeroDeathProbabilityInternal.13.guard-20 | heroes.014 | Source predicate is true: hero.Age &gt; (float)becomeOldAge | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num3, num4, num | True-branch calls: MathF.Pow | TaleWorlds.CampaignSystem.GameComponents.DefaultHeroDeathProbabilityCalculationModel@13-35 |
| DefaultHeroDeathProbabilityCalculationModel.CalculateHeroDeathProbabilityInternal.13.guard-22 | heroes.014 | Source predicate is true: hero.Age &lt; (float)num2 | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num3, num4, num | True-branch calls: MathF.Pow | TaleWorlds.CampaignSystem.GameComponents.DefaultHeroDeathProbabilityCalculationModel@13-35 |
| DefaultHeroDeathProbabilityCalculationModel.CalculateHeroDeathProbabilityInternal.13.guard-28 | heroes.014 | Source predicate is true: hero.Age &gt;= (float)num2 | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: num | True-branch calls: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultHeroDeathProbabilityCalculationModel@13-35 |
