using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using Helpers;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.LinQuick;
using EmptyClanParty = TaleWorlds.CampaignSystem.CampaignBehaviors.EmptyClanPartiesCampaignBehavior.EmptyClanParty;

namespace GameInterface.Services.Clans.Interfaces;

public interface IEmptyClanPartiesCampaignBehaviorInterface : IGameAbstraction
{
    void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyerParty);
    void OnBeforeHeroKilled(Hero victim);
    void OnMobilePartyCreated(MobileParty party);
    void TryCreateNewClanMobilePartyFromCachedEmptyParties(Hero hero, Settlement settlement);
    void TransferCachedLordPartyToNewParty(Hero cachedPartyLeader, PartyBase newParty);
    void DisbandCachedLordParty(Hero cachedPartyLeader);
    void AddEmptyClanParty(Hero leader, IEnumerable<Ship> ships);
    void RemoveEmptyClanParty(Hero leader);
}

public class EmptyClanPartiesCampaignBehaviorInterface : IEmptyClanPartiesCampaignBehaviorInterface
{
    private readonly IMessageBroker messageBroker;

    public EmptyClanPartiesCampaignBehaviorInterface(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    private static List<EmptyClanParty> EmptyClanParties =>
        Campaign.Current?.GetCampaignBehavior<EmptyClanPartiesCampaignBehavior>()?._emptyClanParties;

    public void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyerParty)
    {
        Hero owner = party.Owner;
        if (owner == null || !owner.Clan.IsPlayerClan() || owner.IsPlayerHero() || owner.IsDead || party.IsCaravan || party.IsDisbanding || destroyerParty == null) return;

        var emptyClanParty = new EmptyClanParty(owner);
        if (!party.IsCurrentlyAtSea)
        {
            for (int i = party.Ships.Count - 1; i >= 0; i--)
            {
                Ship ship = party.Ships[i];
                ship.Owner = null;
                emptyClanParty.Ships.Add(ship);
            }
        }

        EmptyClanParties.Add(emptyClanParty);
        messageBroker.Publish(this, new EmptyClanPartyAdded(owner, emptyClanParty.Ships.ToArray()));
    }

    public void OnBeforeHeroKilled(Hero victim)
    {
        if (!victim.Clan.IsPlayerClan() || victim.IsPlayerHero() || !TryRemoveEmptyClanParty(victim, out var emptyClanParty)) return;

        DistributePartyShipsAndRecoverGold(emptyClanParty);
    }

    public void OnMobilePartyCreated(MobileParty party)
    {
        if (!party.ActualClan.IsPlayerClan() || party.LeaderHero == null || party.IsCaravan || party.IsPlayerParty()) return;
        if (!TryRemoveEmptyClanParty(party.LeaderHero, out var emptyClanParty)) return;

        foreach (Ship ship in emptyClanParty.Ships)
        {
            ChangeShipOwnerAction.ApplyByTransferring(party.Party, ship);
        }
    }

    public void TryCreateNewClanMobilePartyFromCachedEmptyParties(Hero hero, Settlement settlement)
    {
        if (hero == null || !hero.IsActive || !hero.Clan.IsPlayerClan() || hero.IsPlayerHero() || hero.PartyBelongedTo != null) return;
        if (!TryRemoveEmptyClanParty(hero, out var emptyClanParty)) return;

        MobileParty mobileParty = MobilePartyHelper.SpawnLordParty(hero, settlement);
        foreach (Ship ship in emptyClanParty.Ships)
        {
            ChangeShipOwnerAction.ApplyByTransferring(mobileParty.Party, ship);
        }
    }

    public void TransferCachedLordPartyToNewParty(Hero cachedPartyLeader, PartyBase newParty)
    {
        if (!TryRemoveEmptyClanParty(cachedPartyLeader, out var emptyClanParty)) return;

        foreach (Ship ship in emptyClanParty.Ships)
        {
            ChangeShipOwnerAction.ApplyByTransferring(newParty, ship);
        }
    }

    public void DisbandCachedLordParty(Hero cachedPartyLeader)
    {
        if (!TryRemoveEmptyClanParty(cachedPartyLeader, out var emptyClanParty)) return;

        DistributePartyShipsAndRecoverGold(emptyClanParty);
    }

    public void AddEmptyClanParty(Hero leader, IEnumerable<Ship> ships)
    {
        var emptyClanParty = new EmptyClanParty(leader);
        emptyClanParty.Ships.AddRange(ships);
        EmptyClanParties?.Add(emptyClanParty);
    }

    public void RemoveEmptyClanParty(Hero leader)
    {
        var emptyClanParties = EmptyClanParties;
        int index = emptyClanParties?.FindIndex(x => x.Leader == leader) ?? -1;
        if (index >= 0) emptyClanParties.RemoveAt(index);
    }

    private bool TryRemoveEmptyClanParty(Hero leader, out EmptyClanParty emptyClanParty)
    {
        var emptyClanParties = EmptyClanParties;
        int index = emptyClanParties?.FindIndex(x => x.Leader == leader) ?? -1;
        if (index < 0)
        {
            emptyClanParty = default;
            return false;
        }

        emptyClanParty = emptyClanParties[index];
        emptyClanParties.RemoveAt(index);
        messageBroker.Publish(this, new EmptyClanPartyRemoved(leader));
        return true;
    }

    private void DistributePartyShipsAndRecoverGold(EmptyClanParty emptyClanParty)
    {
        Clan clan = emptyClanParty.Leader.Clan;
        List<Ship> ships = emptyClanParty.Ships;
        for (int i = ships.Count - 1; i >= 0; i--)
        {
            MobileParty receiver = ShipHelper.GetClanPartyToGetAvailableShip(ships[i], clan, out bool doesPartyNeedShips);
            if (receiver == null || !doesPartyNeedShips) continue;

            ChangeShipOwnerAction.ApplyByTransferring(receiver.Party, ships[i]);
            ships.RemoveAt(i);
        }

        if (ships.Count == 0) return;

        // ShipHelper only applies the selling penalty for Clan.PlayerClan, which is never a player's clan on the server
        var shipCostModel = Campaign.Current.Models.ShipCostModel;
        PartyBase seller = clan.Leader.PartyBelongedTo?.Party;
        int gold = (int)ships.SumQ(ship => shipCostModel.GetShipTradeValue(ship, seller, null));
        gold = (int)(gold * shipCostModel.GetShipSellingPenalty());

        GiveGoldAction.ApplyBetweenCharacters(null, clan.Leader, gold);
        messageBroker.Publish(this, new NotifyEmptyClanPartyShipsRecovered(clan.Leader, emptyClanParty.Leader, gold));
    }
}
