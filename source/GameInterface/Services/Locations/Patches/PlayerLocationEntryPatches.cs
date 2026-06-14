using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Locations.Messages;
using HarmonyLib;
using SandBox;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Detects the local player entering a settlement interior (the indoor mission opening) and raises
/// <see cref="PlayerEnteredLocation"/>. This is the front of the location-sync handoff: it is what
/// kicks off the request to the server for a P2P instance.
///
/// Both <see cref="SandBoxMissions.OpenIndoorMission"/> overloads carry the entered
/// <see cref="Location"/> and return the opened <see cref="Mission"/>, so both are patched.
/// </summary>
[HarmonyPatch(typeof(SandBoxMissions))]
internal class PlayerLocationEntryPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerLocationEntryPatches>();

    [HarmonyPatch(nameof(SandBoxMissions.OpenIndoorMission),
        new[] { typeof(string), typeof(int), typeof(Location), typeof(CharacterObject) })]
    [HarmonyPostfix]
    static void OpenIndoorMission_WithUpgradeLevel(string scene, Location location, Mission __result)
    {
        Handle(scene, location, __result);
    }

    [HarmonyPatch(nameof(SandBoxMissions.OpenIndoorMission),
        new[] { typeof(string), typeof(Location), typeof(CharacterObject), typeof(string) })]
    [HarmonyPostfix]
    static void OpenIndoorMission_WithSceneLevels(string scene, Location location, Mission __result)
    {
        Handle(scene, location, __result);
    }

    private static void Handle(string scene, Location location, Mission mission)
    {
        // The interior is owned locally by the player who entered it; the server has no main party
        // walking into a tavern, so it never requests an instance for itself.
        if (ModInformation.IsServer)
        {
            Logger.Debug("[LocationSync] OpenIndoorMission('{Scene}') on server — no instance request", scene);
            return;
        }

        if (location == null)
        {
            Logger.Warning("[LocationSync] OpenIndoorMission('{Scene}') opened with a null Location — cannot request an instance", scene);
            return;
        }

        var settlement = Settlement.CurrentSettlement;

        Logger.Information(
            "[LocationSync] Local player entered location '{Location}' (scene '{Scene}') in settlement '{Settlement}'; mission={MissionOpened}. Requesting P2P instance.",
            location.StringId, scene, settlement?.StringId ?? "<null>", mission != null);

        MessageBroker.Instance.Publish(location, new PlayerEnteredLocation(settlement, location));
    }
}
