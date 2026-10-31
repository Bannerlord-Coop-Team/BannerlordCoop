using Common;
using Common.Network;
using GameInterface;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

[HarmonyPatch(typeof(CampaignMission))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalRaidMissionPatches
{
    // Naval village raids are single player for now, so the server holds the battle before this client opens vanilla's mission
    [HarmonyPatch(nameof(CampaignMission.OpenNavalRaidMission))]
    [HarmonyPrefix]
    private static bool OpenNavalRaidMissionPrefix(ref IMission __result, out string __state)
    {
        __state = null;
        if (ModInformation.IsServer) return true;

        __result = null;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetIdWithLogging(PlayerEncounter.Battle, out var mapEventId) ||
            !objectManager.TryGetIdWithLogging(MobileParty.MainParty, out var partyId)) return false;

        if (BattleStartCoordinator.Instance?.RequestBlocking(BattleStartMode.Mission, mapEventId, partyId) != true) return false;

        __state = mapEventId;
        return true;
    }

    [HarmonyPatch(nameof(CampaignMission.OpenNavalRaidMission))]
    [HarmonyPostfix]
    private static void OpenNavalRaidMissionPostfix(IMission __result, string __state)
    {
        if (__state == null || __result is not Mission mission) return;
        if (!ContainerProvider.TryResolve<INetwork>(out var network) ||
            !ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return;

        network.SendAll(new NetworkMissionEntered(controllerIdProvider.ControllerId, __state));
        mission.AddMissionBehavior(new NavalRaidMissionMembershipLogic(network, controllerIdProvider.ControllerId, __state));
    }
}

/// <summary>
/// [Client] Leaves the mission instance the single-player naval raid mission entered, so the server releases the battle.
/// A disconnect releases it the same way.
/// </summary>
internal class NavalRaidMissionMembershipLogic : MissionLogic
{
    private readonly INetwork network;
    private readonly string controllerId;
    private readonly string mapEventId;

    public NavalRaidMissionMembershipLogic(INetwork network, string controllerId, string mapEventId)
    {
        this.network = network;
        this.controllerId = controllerId;
        this.mapEventId = mapEventId;
    }

    public override void OnEndMissionInternal()
    {
        network.SendAll(new NetworkMissionLeft(controllerId, mapEventId));
        base.OnEndMissionInternal();
    }
}
