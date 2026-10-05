# DefaultBuildingScoreCalculationModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingScoreCalculationModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultBuildingScoreCalculationModel.GetNextDailyBuilding.11.member | buildings.001 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return town.Buildings.GetRandomElementWithPredicate((Building b) =&gt; b.BuildingType.IsDailyProject); | Locally assigned targets: none located | Calls in this member: town.Buildings.GetRandomElementWithPredicate | TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingScoreCalculationModel@11-14 |
| DefaultBuildingScoreCalculationModel.GetNextBuilding.16.member | buildings.001 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return town.Buildings.WhereQ((Building x) =&gt; !x.BuildingType.IsDailyProject &amp;&amp; x.CurrentLevel &lt; 3 &amp;&amp; !town.BuildingsInProgress.Contains(x)).GetRandomElementInefficiently(); | Locally assigned targets: none located | Calls in this member: town.Buildings.WhereQ, town.BuildingsInProgress.Contains, GetRandomElementInefficiently | TaleWorlds.CampaignSystem.GameComponents.DefaultBuildingScoreCalculationModel@16-19 |
