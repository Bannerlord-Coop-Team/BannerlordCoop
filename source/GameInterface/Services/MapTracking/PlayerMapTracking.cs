using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.MapTracking.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapTracking;

public interface IPlayerMapTracking : IGameAbstraction
{
    bool IsTrackedByLocalPlayer(MobileParty party);
    bool AreTracking(MobileParty party, MobileParty otherParty);
    bool SetTracking(MobileParty party, MobileParty otherParty, bool isTracking);
    void Apply(string controllerId, string otherControllerId, bool isTracking);
    void RefreshTracker(Player player);
}

internal class PlayerMapTracking : IPlayerMapTracking
{
    private readonly IPlayerManager playerManager;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public PlayerMapTracking(
        IPlayerManager playerManager,
        IControllerIdProvider controllerIdProvider,
        IObjectManager objectManager,
        INetwork network)
    {
        this.playerManager = playerManager;
        this.controllerIdProvider = controllerIdProvider;
        this.objectManager = objectManager;
        this.network = network;
    }

    public bool IsTrackedByLocalPlayer(MobileParty party) =>
        TryGetPlayer(party, out var player) &&
        playerManager.TryGetPlayer(controllerIdProvider.ControllerId, out var localPlayer) &&
        AreTracking(player, localPlayer);

    public bool AreTracking(MobileParty party, MobileParty otherParty) =>
        TryGetPlayer(party, out var player) &&
        TryGetPlayer(otherParty, out var otherPlayer) &&
        AreTracking(player, otherPlayer);

    // Both sides must list each other, a recreated character starts with an empty list
    private static bool AreTracking(Player player, Player otherPlayer) =>
        player.MapTrackedControllerIds.Contains(otherPlayer.ControllerId) &&
        otherPlayer.MapTrackedControllerIds.Contains(player.ControllerId);

    public bool SetTracking(MobileParty party, MobileParty otherParty, bool isTracking)
    {
        if (!TryGetPlayer(party, out var player) || !TryGetPlayer(otherParty, out var otherPlayer)) return false;

        Apply(player.ControllerId, otherPlayer.ControllerId, isTracking);
        network.SendAll(new NetworkPlayerMapTrackingChanged(player.ControllerId, otherPlayer.ControllerId, isTracking));
        return true;
    }

    public void Apply(string controllerId, string otherControllerId, bool isTracking)
    {
        if (controllerId == otherControllerId) return;

        ApplyOneWay(controllerId, otherControllerId, isTracking);
        ApplyOneWay(otherControllerId, controllerId, isTracking);
    }

    public void RefreshTracker(Player player)
    {
        var trackers = Campaign.Current?.MapTrackerManager;
        if (trackers == null || player == null) return;
        if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party)) return;

        // v1.5 Refresh removes the tracker of a party that is no longer tracked and adds one for an eligible party
        trackers.Refresh(party);
    }

    private void ApplyOneWay(string controllerId, string otherControllerId, bool isTracking)
    {
        if (!playerManager.TryGetPlayer(controllerId, out var player)) return;

        var trackedIds = player.MapTrackedControllerIds;
        if (!isTracking) trackedIds.Remove(otherControllerId);
        else if (!trackedIds.Contains(otherControllerId)) trackedIds.Add(otherControllerId);
    }

    private bool TryGetPlayer(MobileParty party, out Player player)
    {
        player = null;
        return party != null &&
            PlayerManager.TryGetControlledObjectInfo(party, out var info) &&
            playerManager.TryGetPlayer(info.ObjectControllerId, out player);
    }
}
