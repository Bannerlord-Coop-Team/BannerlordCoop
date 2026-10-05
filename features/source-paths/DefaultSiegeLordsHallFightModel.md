# DefaultSiegeLordsHallFightModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeLordsHallFightModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultSiegeLordsHallFightModel.GetPriorityListForLordsHallFightMission.27.member | sieges.012 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return flattenedTroopRoster; | Locally assigned targets: list, flattenedTroopRoster, list2, list3, num, count, count2, num2, num3, num4, flattenedTroopRosterElement, flattenedTroopRosterElement2 | Calls in this member: playerMapEvent.PartiesOnSide, ToList, FlattenedTroopRoster, list.Sum, foreach, flattenedTroopRoster.Add, item.Party.MemberRoster.GetTroopRoster, flattenedTroopRoster.Where, list2.Shuffle, list3.Shuffle, flattenedTroopRoster.RemoveIf, flattenedTroopRoster.Count, MathF.Min | TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeLordsHallFightModel@27-69 |
| DefaultSiegeLordsHallFightModel.GetPriorityListForLordsHallFightMission.27.guard-43 | sieges.012 | Source predicate is true: num &gt; 0 | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: count, count2, num2, num3, num4, flattenedTroopRosterElement, flattenedTroopRosterElement2 | True-branch calls: MathF.Min, flattenedTroopRoster.Add | TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeLordsHallFightModel@27-69 |
| DefaultSiegeLordsHallFightModel.GetPriorityListForLordsHallFightMission.27.guard-52 | sieges.012 | Source predicate is true: num3 &lt; num2 | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: flattenedTroopRosterElement | True-branch calls: flattenedTroopRoster.Add | TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeLordsHallFightModel@27-69 |
| DefaultSiegeLordsHallFightModel.GetPriorityListForLordsHallFightMission.27.guard-58 | sieges.012 | Source predicate is true: num4 &lt; count2 &amp;&amp; num &gt; 0 | Local true-branch returns: continues; final outcome requires surrounding control flow | True-branch assignment targets: flattenedTroopRosterElement2 | True-branch calls: flattenedTroopRoster.Add | TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeLordsHallFightModel@27-69 |
