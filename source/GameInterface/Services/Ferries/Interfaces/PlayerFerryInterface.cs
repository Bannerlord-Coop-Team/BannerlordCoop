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

    /// <summary>[Server] Whether the party can take the village's ferry, like FerryCampaignBehavior.game_menu_ferry_target_on_condition.</summary>
    bool CanTakeFerry(MobileParty party, Settlement village, out string reason);

    /// <summary>[Server] Charges the fare, keeps the fleet and puts the party on the ferry at the village's port.</summary>
    void StartFerry(MobileParty party, Settlement village);

    /// <summary>[Server] Lands a party on a ferry at the target village and gives its fleet back. Returns false if it isn't on one.</summary>
    bool TryEndFerry(MobileParty party);

    /// <summary>[Client] Puts this client's party on the village's ferry, so vanilla's ferry menus steer it to the target.</summary>
    void BoardLocally(Settlement village);
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

    public bool CanTakeFerry(MobileParty party, Settlement village, out string reason)
    {
        reason = null;
        if (!TryGetPlayerHero(party, out var playerHero))
        {
            reason = "your party isn't a player's party";
            return false;
        }
        if (village?.FerryTarget == null || party.CurrentSettlement != village)
        {
            reason = "your party isn't at a village with a ferry";
            return false;
        }
        if (party.IsCurrentlyAtSea || IsOnFerry(party))
        {
            reason = "your party is already at sea";
            return false;
        }
        if (party.Army != null)
        {
            reason = "you cannot use ferries while you are part of an army";
            return false;
        }

        var ferryModel = Campaign.Current.Models.FerryModel;
        if (party.MemberRoster.TotalManCount + party.PrisonRoster.TotalManCount > ferryModel.MaximumFerryCapacityForPassengers)
        {
            reason = "the number of troops and prisoners exceeds the ferry's capacity";
            return false;
        }
        if (party.PartyTradeGold < GetFare(playerHero, party, village))
        {
            reason = "you don't have enough gold to take the ferry";
            return false;
        }

        return true;
    }

    // Same as FerryCampaignBehavior.game_menu_ferry_target_on_consequence for this player's party
    public void StartFerry(MobileParty party, Settlement village)
    {
        if (!TryGetPlayerHero(party, out var playerHero)) return;
        if (!playerManager.TryGetPlayer(party, out var player)) return;
        if (!objectManager.TryGetIdWithLogging(village, out var villageId)) return;

        GiveGoldAction.ApplyBetweenCharacters(playerHero, null, (int)GetFare(playerHero, party, village), false);

        raftFleetInterface.CacheFleet(party);
        PlayerFerryDepartures[player.HeroId] = villageId;
        messageBroker.Publish(this, new PlayerFerryChanged(player.HeroId, villageId));

        party.Position = village.PortPosition;
        party.IsCurrentlyAtSea = true;
        party.IgnoreByOtherPartiesTill(CampaignTime.Never);
        // Vanilla steers to the target village, the port stops the ferry where the client's menu ends the trip instead of entering it
        party.SetMoveGoToPoint(village.FerryTarget.PortPosition, MobileParty.NavigationType.Naval);
        party.Ai.DisableAi();
        party.Party.SetCustomName(new TextObject("{=avb1Niqp}Ferry"));

        // The disabled AI never publishes the move, so this replicates it with the port position
        messageBroker.Publish(this, new PartyBehaviorChangeAttempted(party, forcePosition: true, isCurrentlyAtSea: true));
    }

    // Same as FerryCampaignBehavior.player_ferry_state_end_continue_on_consequence for this player's party
    public bool TryEndFerry(MobileParty party)
    {
        if (!playerManager.TryGetPlayer(party, out var player)) return false;
        if (!PlayerFerryDepartures.TryGetValue(player.HeroId, out var villageId)) return false;
        if (!objectManager.TryGetObjectWithLogging<Settlement>(villageId, out var village) || village.FerryTarget == null) return false;

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
        return true;
    }

    // The server keeps the fleet, so the client's ferry has no reserved ships for vanilla to give back
    public void BoardLocally(Settlement village)
    {
        Campaign.Current.PlayerDataForNavalAutoTravel = new PlayerDataForNavalAutoTravel(new MBList<Ship>(), null, village);

        // Leaving the village holds the party after the server's ferry move arrived, so start the same move locally
        using (new AllowedThread())
        {
            MobileParty.MainParty.SetMoveGoToPoint(village.FerryTarget.PortPosition, MobileParty.NavigationType.Naval);
        }

        GameMenu.ActivateGameMenu("player_ferry_state_wait");
    }

    // NavalFerryModel prices the fare from Clan.PlayerClan and MobileParty.MainParty
    private static float GetFare(Hero playerHero, MobileParty party, Settlement village)
    {
        using (new MainHeroSubstitutionScope(playerHero, party))
        {
            return Campaign.Current.Models.FerryModel.GetFerryCost(village).ResultNumber;
        }
    }

    private bool TryGetPlayerHero(MobileParty party, out Hero playerHero)
    {
        playerHero = null;
        return playerManager.TryGetPlayer(party, out var player) && objectManager.TryGetObject(player.HeroId, out playerHero);
    }
}
