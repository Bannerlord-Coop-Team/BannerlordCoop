using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Locations.Messages;
using HarmonyLib;
using SandBox;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Raises <see cref="PlayerEnteredLocation"/> when the local player enters a location, kicking off the
/// P2P instance request. The game opens each location type through a different <see cref="SandBoxMissions"/>
/// method (per <c>LocationEncounter.CreateAndOpenMissionController</c>) — indoor (tavern/hall), town centre,
/// castle courtyard, village — and all route both menu entry and in-mission door transitions, so each is
/// patched. The non-indoor entries patch only the <c>string sceneLevels</c> leaf (the <c>int</c> overload
/// delegates to it) to fire once per entry.
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

    [HarmonyPatch(nameof(SandBoxMissions.OpenTownCenterMission),
        new[] { typeof(string), typeof(string), typeof(Location), typeof(CharacterObject), typeof(string) })]
    [HarmonyPostfix]
    static void OpenTownCenterMission_Postfix(string scene, Location location, Mission __result)
    {
        Handle(scene, location, __result);
    }

    [HarmonyPatch(nameof(SandBoxMissions.OpenCastleCourtyardMission),
        new[] { typeof(string), typeof(string), typeof(Location), typeof(CharacterObject) })]
    [HarmonyPostfix]
    static void OpenCastleCourtyardMission_Postfix(string scene, Location location, Mission __result)
    {
        Handle(scene, location, __result);
    }

    [HarmonyPatch(nameof(SandBoxMissions.OpenVillageMission),
        new[] { typeof(string), typeof(Location), typeof(CharacterObject), typeof(string) })]
    [HarmonyPostfix]
    static void OpenVillageMission_Postfix(string scene, Location location, Mission __result)
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

        // Attach the P2P location behaviors to the freshly opened mission FIRST so the controller is alive
        // and subscribed before PlayerEnteredLocation is published — it owns the instance request and the
        // PeerConnected / join-info exchange. Doing it here (at mission open) rather than on InstanceAssigned
        // avoids the race where the assignment round-trips back before the interior scene has loaded.
        AttachLocationBehaviors(mission);

        MessageBroker.Instance.Publish(location, new PlayerEnteredLocation(settlement, location));
    }

    // GameInterface cannot reference the Missions P2P behaviors directly (Missions depends on
    // GameInterface), so resolve them from the shared container as ILocationMissionBehavior and add them
    // to the mission. Runs on the game thread (Harmony postfix on the engine call).
    private static void AttachLocationBehaviors(Mission mission)
    {
        if (mission == null) return;

        // OpenIndoorMission fires several times per entry; the tracker dedupes so we attach exactly once.
        if (LocationMissionTracker.IsLocationMission(mission)) return;

        if (ContainerProvider.TryResolve(out IEnumerable<ILocationMissionBehavior> behaviors) == false)
        {
            Logger.Warning("[LocationSync] Mission container not available — cannot attach P2P location behaviors to '{Scene}'", mission.SceneName);
            return;
        }

        LocationMissionTracker.TryRegister(mission);

        foreach (var behavior in behaviors)
        {
            if (behavior is MissionBehavior missionBehavior)
            {
                mission.AddMissionBehavior(missionBehavior);
                Logger.Information("[LocationSync] Attached {Behavior} to mission '{Scene}'", behavior.GetType().Name, mission.SceneName);
            }
        }
    }
}
