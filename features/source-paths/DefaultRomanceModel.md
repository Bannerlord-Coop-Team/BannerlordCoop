# DefaultRomanceModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultRomanceModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultRomanceModel.GetAttractionValuePercentage.8.member | heroes.008 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return MathF.Abs((potentiallyInterestedCharacter.StaticBodyProperties.GetHashCode() + heroOfInterest.StaticBodyProperties.GetHashCode()) % 100); | Locally assigned targets: none located | Calls in this member: MathF.Abs, potentiallyInterestedCharacter.StaticBodyProperties.GetHashCode, heroOfInterest.StaticBodyProperties.GetHashCode | TaleWorlds.CampaignSystem.GameComponents.DefaultRomanceModel@8-11 |
