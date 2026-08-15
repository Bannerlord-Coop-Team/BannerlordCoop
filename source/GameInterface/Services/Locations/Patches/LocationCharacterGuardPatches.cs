using Common;
using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using SandBox;
using SandBox.Missions.AgentBehaviors;
using SandBox.Missions.MissionLogics;
using SandBox.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Client-side guards around the code paths that move characters between locations outside the
/// patched <see cref="Location"/> mutators.
/// </summary>
[HarmonyPatch]
internal class LocationCharacterGuardPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationCharacterGuardPatches>();

    // On clients, settlement-NPC hero moves come from server broadcasts. The local player's own
    // accompanying companions are the exception: vanilla owns their location/AI lifecycle on this client.
    // Blocking other hero moves prevents ghost agents that have no authoritative roster entry.
    [HarmonyPatch(typeof(LocationComplex), nameof(LocationComplex.ChangeLocation))]
    [HarmonyPrefix]
    static bool ChangeLocationPrefix(LocationCharacter locationCharacter)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        return locationCharacter?.Character?.IsHero != true || IsLocalPlayerPartyCharacter(locationCharacter);
    }

    // Let vanilla spawn and drive this client's accompanying companions. Other clients never run this
    // local player's LocationEncounter; they receive the companions as controller-less P2P puppets.
    [HarmonyPatch(typeof(MissionLocationLogic), nameof(MissionLocationLogic.SpawnCharactersAccompanyingPlayer))]
    [HarmonyPrefix]
    static bool SpawnCharactersAccompanyingPlayerPrefix(bool noHorse)
    {
        bool shouldSpawn = CallOriginalPolicy.IsOriginalAllowed() ||
            ShouldSpawnAccompanyingCharacters(ModInformation.IsClient);

        if (ModInformation.IsClient)
        {
            EnsureVanillaAccompanyingCharacter();
            LogVanillaCompanionSpawnInput(noHorse, shouldSpawn);
        }

        return shouldSpawn;
    }

    [HarmonyPatch(typeof(MissionLocationLogic), nameof(MissionLocationLogic.SpawnCharactersAccompanyingPlayer))]
    [HarmonyPostfix]
    static void SpawnCharactersAccompanyingPlayerPostfix()
    {
        if (ModInformation.IsClient)
            LogVanillaCompanionSpawnResult();
    }

    internal static bool ShouldSpawnAccompanyingCharacters(bool isClient) => isClient;

    /// <summary>
    /// Vanilla normally delegates bodyguard selection to ClanMemberRolesCampaignBehavior, but that broad
    /// campaign behavior is intentionally disabled in co-op. Reproduce only its location-mission responsibility:
    /// add the first eligible MainParty hero when the encounter has no player-party companion entry. Other
    /// accompanying characters (for example quest followers) are preserved.
    /// </summary>
    private static void EnsureVanillaAccompanyingCharacter()
    {
        try
        {
            LocationEncounter encounter = PlayerEncounter.LocationEncounter;
            MobileParty mainParty = MobileParty.MainParty;
            LocationComplex locationComplex = LocationComplex.Current;
            if (encounter == null || mainParty?.MemberRoster == null || locationComplex == null) return;

            foreach (AccompanyingCharacter existing in encounter.CharactersAccompanyingPlayer)
            {
                if (existing?.LocationCharacter?.AgentOrigin is PartyAgentOrigin origin &&
                    origin.Party == PartyBase.MainParty)
                {
                    return;
                }
            }

            foreach (var element in mainParty.MemberRoster.GetTroopRoster())
            {
                CharacterObject character = element.Character;
                Hero hero = character?.HeroObject;
                if (hero == null || !IsEligibleVanillaCompanion(
                        character.IsHero,
                        hero == Hero.MainHero,
                        hero.IsPrisoner,
                        hero.IsWounded,
                        hero.Age,
                        Campaign.Current.Models.AgeModel.HeroComesOfAge)) continue;

                LocationCharacter entry = LocationCharacter.CreateBodyguardHero(
                    hero,
                    mainParty,
                    SandBoxManager.Instance.AgentBehaviorManager.AddFirstCompanionBehavior);
                encounter.AddAccompanyingCharacter(entry, isFollowing: true);

                AccompanyingCharacter accompanying = encounter.GetAccompanyingCharacter(character);
                accompanying?.DisallowEntranceToAllLocations();
                accompanying?.AllowEntranceToLocations(location =>
                    location == locationComplex.GetLocationWithId("center") ||
                    location == locationComplex.GetLocationWithId("village_center") ||
                    location == locationComplex.GetLocationWithId("tavern"));

                Logger.Warning(
                    "[LocationCompanion] Reconstructed vanilla bodyguard entry for {Character}; no MainParty companion had been added to this encounter",
                    character.StringId);
                break;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[LocationCompanion] Failed to reconstruct vanilla accompanying entry");
        }
    }

    internal static bool IsEligibleVanillaCompanion(
        bool isHero,
        bool isMainHero,
        bool isPrisoner,
        bool isWounded,
        float age,
        float heroComesOfAge)
        => isHero && !isMainHero && !isPrisoner && !isWounded && age >= heroComesOfAge;

    private static void LogVanillaCompanionSpawnInput(bool noHorse, bool shouldSpawn)
    {
        try
        {
            LocationEncounter encounter = PlayerEncounter.LocationEncounter;
            Location currentLocation = CampaignMission.Current?.Location;
            var partyHeroDetails = new List<string>();
            var accompanyingDetails = new List<string>();

            MobileParty mainParty = MobileParty.MainParty;
            if (mainParty?.MemberRoster != null)
            {
                foreach (var element in mainParty.MemberRoster.GetTroopRoster())
                {
                    CharacterObject character = element.Character;
                    if (character?.IsHero != true || character == CharacterObject.PlayerCharacter) continue;

                    Hero hero = character.HeroObject;
                    partyHeroDetails.Add($"{character.StringId}(count={element.Number},wounded={hero?.IsWounded},prisoner={hero?.IsPrisoner},alive={hero?.IsAlive})");
                }
            }

            var accompanying = encounter?.CharactersAccompanyingPlayer;
            if (accompanying != null)
            {
                foreach (AccompanyingCharacter companion in accompanying)
                {
                    LocationCharacter entry = companion.LocationCharacter;
                    bool canEnter = currentLocation != null && companion.CanEnterLocation(currentLocation);
                    bool inRoster = currentLocation?.GetCharacterList()?.Contains(entry) == true;
                    accompanyingDetails.Add($"{entry?.Character?.StringId ?? "<null>"}(following={companion.IsFollowingPlayerAtMissionStart},canEnter={canEnter},inRoster={inRoster},wounded={entry?.Character?.HeroObject?.IsWounded})");
                }
            }

            Logger.Information(
                "[LocationCompanion] Vanilla accompanying spawn input: allowed={Allowed}, noHorse={NoHorse}, settlement={Settlement}, location={Location}, encounterEntries={EncounterCount} [{EncounterEntries}], mainPartyHeroes={PartyHeroCount} [{PartyHeroes}]",
                shouldSpawn,
                noHorse,
                encounter?.Settlement?.StringId ?? "<null>",
                currentLocation?.StringId ?? "<null>",
                accompanying?.Count ?? 0,
                string.Join(", ", accompanyingDetails),
                partyHeroDetails.Count,
                string.Join(", ", partyHeroDetails));

            if (shouldSpawn && partyHeroDetails.Count > 0 && (accompanying == null || accompanying.Count == 0))
            {
                Logger.Warning(
                    "[LocationCompanion] MainParty contains companion heroes but vanilla LocationEncounter has no accompanying entries. ClanMemberRolesCampaignBehavior likely did not populate the encounter before mission open.");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[LocationCompanion] Failed to inspect vanilla accompanying spawn input");
        }
    }

    private static void LogVanillaCompanionSpawnResult()
    {
        try
        {
            Mission mission = Mission.Current;
            Location currentLocation = CampaignMission.Current?.Location;
            LocationEncounter encounter = PlayerEncounter.LocationEncounter;
            var ownedAgentDetails = new List<string>();
            var missingCharacters = new List<string>();

            if (mission != null)
            {
                foreach (Agent agent in mission.Agents)
                {
                    if (ReferenceEquals(agent, Agent.Main)) continue;
                    if (!(agent.Origin is PartyAgentOrigin origin) || origin.Party != PartyBase.MainParty) continue;

                    ownedAgentDetails.Add($"{agent.Character?.StringId ?? "<null>"}(active={agent.IsActive()},health={agent.Health:0.##},controller={agent.Controller})");
                }
            }

            if (encounter?.CharactersAccompanyingPlayer != null && currentLocation != null)
            {
                foreach (AccompanyingCharacter companion in encounter.CharactersAccompanyingPlayer)
                {
                    LocationCharacter entry = companion.LocationCharacter;
                    bool woundedOutsideRoster = entry?.Character?.HeroObject?.IsWounded == true &&
                        currentLocation.GetCharacterList()?.Contains(entry) != true;
                    if (!companion.CanEnterLocation(currentLocation) || woundedOutsideRoster) continue;

                    bool found = false;
                    if (mission != null)
                    {
                        foreach (Agent agent in mission.Agents)
                        {
                            if (agent.Character == entry?.Character)
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                        missingCharacters.Add(entry?.Character?.StringId ?? "<null>");
                }
            }

            Logger.Information(
                "[LocationCompanion] Vanilla accompanying spawn result: location={Location}, ownedCompanionAgents={AgentCount} [{Agents}]",
                currentLocation?.StringId ?? "<null>",
                ownedAgentDetails.Count,
                string.Join(", ", ownedAgentDetails));

            if (missingCharacters.Count > 0)
            {
                Logger.Warning(
                    "[LocationCompanion] Vanilla accompanying entries were eligible but produced no mission agent: [{Characters}]",
                    string.Join(", ", missingCharacters));
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[LocationCompanion] Failed to inspect vanilla accompanying spawn result");
        }
    }

    internal static bool IsLocalPlayerPartyCharacter(LocationCharacter locationCharacter)
    {
        return locationCharacter?.AgentOrigin is PartyAgentOrigin origin && origin.Party == PartyBase.MainParty;
    }

    // On clients an ambient NPC's origin isn't always in the location's character list, so vanilla's
    // GetLocationCharacter returns null and the door-picking AI NREs on locationCharacter.FixedLocation.
    // Report the passage as unavailable so the agent just skips the door this tick.
    [HarmonyPatch(typeof(ChangeLocationBehavior), nameof(ChangeLocationBehavior.GetAvailability))]
    [HarmonyPrefix]
    static bool GetAvailabilityPrefix(ChangeLocationBehavior __instance, ref float __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        if (CampaignMission.Current?.Location?.GetLocationCharacter(__instance.OwnerAgent.Origin) != null) return true;

        __result = 0f;
        return false;
    }

    // Same null location-character on the client reaches the passage AI, which hands it to
    // LocationComplex.CanIfMaleOrHero and NREs on locationCharacter.Character. Treat the passage as
    // disabled for that agent so it doesn't path through the door.
    [HarmonyPatch(typeof(PassageUsePoint), nameof(PassageUsePoint.IsDisabledForAgent))]
    [HarmonyPrefix]
    static bool IsDisabledForAgentPrefix(PassageUsePoint __instance, Agent agent, ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsServer) return true;

        if (!agent.IsAIControlled || __instance.IsMissionExit || __instance.ToLocation == null) return true;
        if (CampaignMission.Current?.Location?.GetLocationCharacter(agent.Origin) != null) return true;

        __result = true;
        return false;
    }
}
