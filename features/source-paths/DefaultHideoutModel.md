# DefaultHideoutModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultHideoutModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultHideoutModel.GetRogueryXpGainAsGhost.15.member | ai.009 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return MBRandom.RandomFloatRanged(1000f, 1400f); | Locally assigned targets: none located | Calls in this member: MBRandom.RandomFloatRanged | TaleWorlds.CampaignSystem.GameComponents.DefaultHideoutModel@15-18 |
| DefaultHideoutModel.GetRogueryXpGainOnHideoutMissionEnd.20.member | ai.009 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return isSucceeded ? MBRandom.RandomInt(700, 1000) : MBRandom.RandomInt(225, 400); | Locally assigned targets: none located | Calls in this member: MBRandom.RandomInt | TaleWorlds.CampaignSystem.GameComponents.DefaultHideoutModel@20-23 |
| DefaultHideoutModel.GetSendTroopsSuccessChance.25.member | ai.009 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return 0.3f + (float)(skillValue + skillValue2) / (325f + (float)skillValue + (float)skillValue2); | Locally assigned targets: skillValue, skillValue2 | Calls in this member: Hero.MainHero.GetSkillValue | TaleWorlds.CampaignSystem.GameComponents.DefaultHideoutModel@25-30 |
