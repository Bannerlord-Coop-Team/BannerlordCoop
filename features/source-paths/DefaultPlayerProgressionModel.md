# DefaultPlayerProgressionModel

Installed type: `TaleWorlds.CampaignSystem.GameComponents.DefaultPlayerProgressionModel`.

Normal support: unverified.
[Index](../source-paths.md) · [Mapping contract](../granularity.md) · [Source identities](../baseline/behavior-sources.json)

This is structural navigation. Local returns, assignments and calls can belong to nested
or exclusive branches; resolve the complete caller/guard context before using any as an outcome.

| Path | Parent | Predicate / entry | Local outcome | Assignment targets | Call targets | Source span |
| --- | --- | --- | --- | --- | --- | --- |
| DefaultPlayerProgressionModel.GetPlayerProgress.9.member | progression.001 | Actual arguments and enclosing type state at member entry; full caller graph unresolved | Named member outcome requires interpretation; local returns: return MBMath.ClampFloat((float)Clan.PlayerClan.Fiefs.Count * 0.1f + Clan.PlayerClan.CurrentTotalStrength * 0.0008f + Clan.PlayerClan.Renown * 1.5E-05f + (float)Clan.PlayerClan.AliveLords.Count * 0.002f + (float)Clan.PlayerClan.Companions.Count * 0.01f + (float)Clan.PlayerClan.SupporterNotables.Count * 0.001f + (float)Hero.MainHero.OwnedCaravans.Count * 0.01f + (float)PartyBase.MainParty.NumberOfAllMembers * 0.002f + (float)CharacterObject.PlayerCharacter.Level * 0.002f, 0f, 1f); | Locally assigned targets: none located | Calls in this member: MBMath.ClampFloat | TaleWorlds.CampaignSystem.GameComponents.DefaultPlayerProgressionModel@9-12 |
