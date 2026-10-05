# DefaultPrisonBreakModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonBreakModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultPrisonBreakModel.GetNumberOfGuardsToSpawn.15.member | prisoners.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return num + num2; | Locally assigned targets: num, num2 | Calls in this member: Math.Ceiling, settlement.Town.GetWallLevel | TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonBreakModel@15-20 |
| DefaultPrisonBreakModel.CanPlayerStagePrisonBreak.22.member | prisoners.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return result; | Locally assigned targets: result, garrisonParty, flag | Calls in this member: DiplomacyHelper.IsSameFactionAndNotEliminated | TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonBreakModel@22-32 |
| DefaultPrisonBreakModel.CanPlayerStagePrisonBreak.22.guard-25 | prisoners.008 | Source predicate is true: settlement.IsFortification | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: garrisonParty, flag, result | True-branch calls: DiplomacyHelper.IsSameFactionAndNotEliminated | TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonBreakModel@22-32 |
| DefaultPrisonBreakModel.GetPrisonBreakStartCost.34.member | prisoners.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return num + 1000; | Locally assigned targets: num | Calls in this member: TaleWorlds.Library.MathF.Ceiling, Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue, Hero.MainHero.GetSkillValue | TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonBreakModel@34-39 |
| DefaultPrisonBreakModel.GetRelationRewardOnPrisonBreak.41.member | prisoners.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return 15; | Locally assigned targets: none located | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonBreakModel@41-44 |
| DefaultPrisonBreakModel.GetRogueryRewardOnPrisonBreak.46.member | prisoners.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return isSuccess ? MBRandom.RandomInt(2000, 4500) : MBRandom.RandomInt(500, 1000); | Locally assigned targets: none located | Calls in this member: MBRandom.RandomInt | TaleWorlds.CampaignSystem.GameComponents.DefaultPrisonBreakModel@46-49 |
