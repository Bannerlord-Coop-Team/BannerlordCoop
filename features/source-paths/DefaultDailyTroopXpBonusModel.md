# DefaultDailyTroopXpBonusModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultDailyTroopXpBonusModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultDailyTroopXpBonusModel.CalculateDailyTroopXpBonus.11.member | party.002 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return CalculateTroopXpBonusInternal(town); | Locally assigned targets: none located | Calls in this member: CalculateTroopXpBonusInternal | TaleWorlds.CampaignSystem.GameComponents.DefaultDailyTroopXpBonusModel@11-14 |
| DefaultDailyTroopXpBonusModel.CalculateTroopXpBonusInternal.16.member | party.002 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return (int)result.ResultNumber; | Locally assigned targets: result | Calls in this member: ExplainedNumber, town.AddEffectOfBuildings, PerkHelper.AddPerkBonusForTown, return | TaleWorlds.CampaignSystem.GameComponents.DefaultDailyTroopXpBonusModel@16-23 |
| DefaultDailyTroopXpBonusModel.CalculateGarrisonXpBonusMultiplier.25.member | party.002 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return 1f; | Locally assigned targets: none located | Calls in this member: none located | TaleWorlds.CampaignSystem.GameComponents.DefaultDailyTroopXpBonusModel@25-28 |
