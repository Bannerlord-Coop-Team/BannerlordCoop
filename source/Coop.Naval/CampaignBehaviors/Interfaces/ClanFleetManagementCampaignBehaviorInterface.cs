using GameInterface.CoopSessionData;
using GameInterface.Services;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players.Interfaces;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Coop.Naval.CampaignBehaviors.Interfaces;

/// <summary>
/// Server-only: ClanFleetManagementCampaignBehavior for every player instead of MobileParty.MainParty.
/// Vanilla keeps one list of sent troops, here each player's is in the coop session.
/// </summary>
public interface IClanFleetManagementCampaignBehaviorInterface : IGameAbstraction
{
    void SendShipToParty(Ship ship, MobileParty mobileParty);
    void HourlyTick();
}

public class ClanFleetManagementCampaignBehaviorInterface : IClanFleetManagementCampaignBehaviorInterface
{
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly IPlayerMessageInterface playerMessageInterface;

    private Dictionary<string, List<SentTroopsData>> PlayerSentTroops => coopSessionProvider.CoopSession.NavalPlayerData.PlayerSentTroops;

    public ClanFleetManagementCampaignBehaviorInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        IPlayerMessageInterface playerMessageInterface)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.playerMessageInterface = playerMessageInterface;
    }

    public void SendShipToParty(Ship ship, MobileParty mobileParty)
    {
        // Same as ClanFleetManagementCampaignBehavior.SendShipToParty, using the player's party that owned the ship
        var sendingParty = ship.Owner?.MobileParty;
        var playerHero = sendingParty?.LeaderHero;
        if (playerHero != null && playerHero.IsPlayerHero() && objectManager.TryGetIdWithLogging(playerHero, out var playerHeroId))
        {
            var troops = sendingParty.MemberRoster.RemoveNumberOfNonHeroTroopsRandomly(Campaign.Current.Models.FleetManagementModel.MinimumTroopCountRequiredToSendShips);
            var sentTroops = new SentTroopsData(GetTroopData(troops), GetReturnTimeForTroops(playerHero)._numTicks, ship.Name.ToString(), mobileParty.Name.ToString());

            if (!PlayerSentTroops.TryGetValue(playerHeroId, out var playerSentTroops))
            {
                playerSentTroops = new List<SentTroopsData>();
                PlayerSentTroops[playerHeroId] = playerSentTroops;
            }
            playerSentTroops.Add(sentTroops);
        }

        ChangeShipOwnerAction.ApplyByTransferring(mobileParty.Party, ship);
    }

    public void HourlyTick()
    {
        // Same as ClanFleetManagementCampaignBehavior.HourlyTick for each player
        foreach (var pair in PlayerSentTroops)
        {
            if (!objectManager.TryGetObject<Hero>(pair.Key, out var playerHero)) continue;

            var party = playerHero.PartyBelongedTo;
            if (party == null || !CanTroopsReturn(playerHero, party)) continue;

            var playerSentTroops = pair.Value;
            for (int i = playerSentTroops.Count - 1; i >= 0; i--)
            {
                if (!new CampaignTime(playerSentTroops[i].ReturnTimeTicks).IsPast) continue;

                MakeTroopsReturn(playerHero, party, playerSentTroops[i]);
                playerSentTroops.RemoveAt(i);
            }
        }
    }

    // Same as NavalDLCFleetManagementModel.CanTroopsReturn for the player instead of Hero.MainHero
    private static bool CanTroopsReturn(Hero playerHero, MobileParty party)
    {
        return !playerHero.IsPrisoner
            && (!party.IsCurrentlyAtSea || party.CurrentSettlement != null)
            && party.MapEvent == null;
    }

    // Same as NavalDLCFleetManagementModel.GetReturnTimeForTroops, seeded with the player instead of Hero.MainHero
    private static CampaignTime GetReturnTimeForTroops(Hero playerHero)
    {
        return CampaignTime.DaysFromNow(playerHero.RandomFloatWithSeed((uint)CampaignTime.Now.ToMinutes, 3f, 6f));
    }

    private void MakeTroopsReturn(Hero playerHero, MobileParty party, SentTroopsData sentTroops)
    {
        foreach (var troop in sentTroops.Troops)
        {
            var character = MBObjectManager.Instance.GetObject<CharacterObject>(troop.CharacterId);
            if (character == null) continue;

            party.MemberRoster.AddToCounts(character, troop.Number, false, troop.WoundedNumber, troop.Xp);
        }

        var message = new TextObject("{=CC5Aa5VH}Your troops have returned from delivering {SHIP_NAME} to {PARTY_NAME}.");
        message.SetTextVariable("SHIP_NAME", sentTroops.ShipName);
        message.SetTextVariable("PARTY_NAME", sentTroops.PartyName);
        playerMessageInterface.SendInformationMessageToHero(playerHero, message.ToString());
    }

    private static List<SentTroopData> GetTroopData(TroopRoster troops)
    {
        var troopData = new List<SentTroopData>();
        foreach (var element in troops.GetTroopRoster())
        {
            troopData.Add(new SentTroopData(element.Character.StringId, element.Number, element.WoundedNumber, element.Xp));
        }

        return troopData;
    }
}
