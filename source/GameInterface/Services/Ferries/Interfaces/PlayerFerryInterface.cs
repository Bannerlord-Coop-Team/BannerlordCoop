using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.Ferries.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Rafts.Interfaces;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Ferries.Interfaces;

/// <summary>
/// The ferry between coastal villages for each player, which vanilla's FerryCampaignBehavior only runs for the main party
/// through the single Campaign.PlayerDataForNavalAutoTravel. The server keeps who is on a ferry and their fleet, and each
/// client sets its own PlayerDataForNavalAutoTravel so vanilla's ferry menus steer its party.
/// </summary>
public interface IPlayerFerryInterface : IGameAbstraction
{
    /// <summary>Whether a player's party is on a ferry.</summary>
    bool IsOnFerry(MobileParty party);

    /// <summary>The village a player's party on a ferry departed from, which picks the ferry's ship.</summary>
    bool TryGetDepartureVillage(MobileParty party, out Settlement village);

    /// <summary>
    /// [Server] Whether the party is still in a village with a ferry and can pay its fare. The rest of
    /// FerryCampaignBehavior.game_menu_ferry_target_on_condition already disables the option on the client.
    /// </summary>
    bool CanTakeFerry(MobileParty party, Settlement village);

    /// <summary>[Server] Charges the fare, keeps the fleet and puts the party on the ferry at the village's port.</summary>
    void StartFerry(MobileParty party, Settlement village);

    /// <summary>[Server] Lands a party on a ferry at the target village and gives its fleet back, if it's still on one.</summary>
    void EndFerry(MobileParty party);

    /// <summary>[Client] Puts this client's party on the village's ferry, so vanilla's ferry menus steer it to the target.</summary>
    void BoardLocally(Settlement village);

    /// <summary>Where the ferry from <paramref name="village"/> takes a party.</summary>
    CampaignVec2 GetFerryDestination(Settlement village);

    /// <summary>Whether a party on the ferry from <paramref name="village"/> reached its destination.</summary>
    bool HasArrived(MobileParty party, Settlement village);
}

/// <inheritdoc cref="IPlayerFerryInterface"/>
public class PlayerFerryInterface : IPlayerFerryInterface
{
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerRaftFleetInterface raftFleetInterface;
    private readonly IPlayerFerryTracker ferryTracker;

    private Dictionary<string, string> PlayerFerryDepartures => coopSessionProvider.CoopSession.NavalPlayerData.PlayerFerryDepartures;

    // A client only has the copy of every player's departures the server sends it
    private IReadOnlyDictionary<string, string> KnownDepartures => ModInformation.IsServer ? PlayerFerryDepartures : ferryTracker.Departures;

    public PlayerFerryInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IMessageBroker messageBroker,
        IPlayerRaftFleetInterface raftFleetInterface,
        IPlayerFerryTracker ferryTracker)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.messageBroker = messageBroker;
        this.raftFleetInterface = raftFleetInterface;
        this.ferryTracker = ferryTracker;
    }

    public bool IsOnFerry(MobileParty party) => TryGetDepartureVillageId(party, out _);

    public bool TryGetDepartureVillage(MobileParty party, out Settlement village)
    {
        village = null;
        return TryGetDepartureVillageId(party, out var villageId) && objectManager.TryGetObjectWithLogging(villageId, out village);
    }

    // Runs on every IsInFerryState read, so the usual case of nobody on a ferry stops at the count
    private bool TryGetDepartureVillageId(MobileParty party, out string villageId)
    {
        villageId = null;
        var departures = KnownDepartures;
        if (departures.Count == 0 || party.LeaderHero == null) return false;

        return objectManager.TryGetId(party.LeaderHero, out var playerHeroId) && departures.TryGetValue(playerHeroId, out villageId);
    }

    public bool CanTakeFerry(MobileParty party, Settlement village) =>
        village?.FerryTarget != null && party.PartyTradeGold >= GetFare(party, village);

    // Same as FerryCampaignBehavior.game_menu_ferry_target_on_consequence for this player's party
    public void StartFerry(MobileParty party, Settlement village)
    {
        if (!playerManager.TryGetPlayer(party, out var player)) return;
        if (!objectManager.TryGetIdWithLogging(village, out var villageId)) return;

        GiveGoldAction.ApplyBetweenCharacters(party.LeaderHero, null, (int)GetFare(party, village), false);

        raftFleetInterface.CacheFleet(party);
        PlayerFerryDepartures[player.HeroId] = villageId;
        messageBroker.Publish(this, new PlayerFerryChanged(player.HeroId, villageId));

        party.Position = village.PortPosition;
        party.IsCurrentlyAtSea = true;
        party.IgnoreByOtherPartiesTill(CampaignTime.Never);
        party.SetMoveGoToPoint(GetFerryDestination(village), MobileParty.NavigationType.Naval);
        party.Ai.DisableAi();
        party.Party.SetCustomName(new TextObject("{=avb1Niqp}Ferry"));

        // The disabled AI never publishes the move, so this replicates it with the port position
        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(party, forcePosition: true, isCurrentlyAtSea: true));
    }

    // Same as FerryCampaignBehavior.player_ferry_state_end_continue_on_consequence for this player's party
    public void EndFerry(MobileParty party)
    {
        if (!playerManager.TryGetPlayer(party, out var player)) return;
        if (!PlayerFerryDepartures.TryGetValue(player.HeroId, out var villageId)) return;
        if (!objectManager.TryGetObjectWithLogging<Settlement>(villageId, out var village)) return;

        PlayerFerryDepartures.Remove(player.HeroId);
        messageBroker.Publish(this, new PlayerFerryChanged(player.HeroId, null));
        var target = village.FerryTarget;
        party.SetMoveModeHold();
        party.Position = target.GatePosition;
        party.IsCurrentlyAtSea = false;
        party.IgnoreByOtherPartiesTill(CampaignTime.HoursFromNow(1f));
        party.Party.SetCustomName(null);
        party.Ai.EnableAi();
        raftFleetInterface.ReturnFleet(party);
        party.Party.SetVisualAsDirty();

        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(party, forcePosition: true, isCurrentlyAtSea: false, resetMovementToHold: true));
    }

    // The server keeps the fleet, so the client's ferry has no reserved ships for vanilla to give back
    public void BoardLocally(Settlement village)
    {
        Campaign.Current.PlayerDataForNavalAutoTravel = new PlayerDataForNavalAutoTravel(new MBList<Ship>(), null, village);

        // Leaving the village holds the party after the server's ferry move arrived, so start the same move locally
        using (new AllowedThread())
        {
            MobileParty.MainParty.SetMoveGoToPoint(GetFerryDestination(village), MobileParty.NavigationType.Naval);
        }

        GameMenu.ActivateGameMenu("player_ferry_state_wait");
    }

    // Vanilla steers into the target village, steering to its port instead stops the ferry where the client's menu ends the trip
    public CampaignVec2 GetFerryDestination(Settlement village) => village.FerryTarget.PortPosition;

    // Same distance as FerryCampaignBehavior.player_ferry_state_menu_on_tick
    public bool HasArrived(MobileParty party, Settlement village) =>
        party.Position.Distance(GetFerryDestination(village)) < Campaign.Current.Models.EncounterModel.NeededMaximumNavalDistanceForEncounteringMobileParty;

    // NavalFerryModel prices the fare from Clan.PlayerClan and MobileParty.MainParty
    private static float GetFare(MobileParty party, Settlement village)
    {
        using (new MainHeroSubstitutionScope(party.LeaderHero, party))
        {
            return Campaign.Current.Models.FerryModel.GetFerryCost(village).ResultNumber;
        }
    }
}
