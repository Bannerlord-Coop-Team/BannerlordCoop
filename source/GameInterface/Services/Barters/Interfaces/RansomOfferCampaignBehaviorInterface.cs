using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.CoopSessionData;
using GameInterface.Services.Barters.Data;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.CampaignService.Statistics.Interfaces;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Barters.Interfaces;

public interface IRansomOfferCampaignBehaviorInterface : IGameAbstraction
{
    void ConsiderRansomPrisoner(Hero hero);
    void HandleDeclineRansomOffer(Hero victim);
    void AcceptRansomOffer(string playerHeroId, string ransomHeroId);
    void DeclineRansomOffer(string playerHeroId, string playerRespondedRansomHeroId = null);
    void OnPrisonersChangeInSettlement(FlattenedTroopRoster roster, Hero prisoner, bool takenFromDungeon);
    void HourlyTick();
    void AddNewPlayerHeroId(string playerHeroId, NetPeer peer);
    void SetClientRansomPrice(int ransomPrice);
    int GetClientRansomPrice();
}

public class RansomOfferCampaignBehaviorInterface : IRansomOfferCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<RansomOfferCampaignBehaviorInterface>();

    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IStatisticsCampaignBehaviorInterface statisticsInterface;

    private Dictionary<string, RansomOfferData> PlayerRansomOfferData => coopSessionProvider.CoopSession.TradePlayerData.PlayerRansomOfferData;

    private int clientRansomPrice;

    public RansomOfferCampaignBehaviorInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IStatisticsCampaignBehaviorInterface statisticsInterface)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.statisticsInterface = statisticsInterface;
    }

    public void ConsiderRansomPrisoner(Hero hero)
    {
        if (!TryGetRansomOfferBehavior(out var ransomOfferBehavior)) return;

        Clan captorClanOfPrisoner = ransomOfferBehavior.GetCaptorClanOfPrisoner(hero);
        if (captorClanOfPrisoner == null) return;

        Hero clanLeader = hero.Clan.Leader;
        Hero ransomPayer = clanLeader != hero
            ? clanLeader
            : hero.Clan.AliveLords.Where(lord => lord != hero).GetRandomElementInefficiently();
        if (ransomPayer.IsPlayerHero() && ransomPayer.IsPrisoner) return;

        bool capturedPlayerClanMember = hero.Clan.IsPlayerClan();
        bool captorPlayerClan = captorClanOfPrisoner.IsPlayerClan();

        // Don't allow automatic player to player ransom offers. Only pass this check for player - NPC or NPC - player interactions.
        if (capturedPlayerClanMember ^ captorPlayerClan)
        {
            // Offers for captured player clan members, player heroes included, go to the clan leader since they manage the finances.
            // A captured leader gets none like vanilla
            if (!objectManager.TryGetIdWithLogging(capturedPlayerClanMember ? hero.Clan.Leader : captorClanOfPrisoner.Leader, out var playerHeroId)) return;
            if (!ValidatePlayerHeroId(playerHeroId, out var playerData, out var playerHero)) return;
            if (!objectManager.TryGetIdWithLogging(hero, out var heroId)) return;

            if (playerData.CurrentRansomHeroId == null && playerHero.PartyBelongedTo?.IsInRaftState == false)
            {
                float chanceToDisplayRansomOffer = (!playerData.HeroIdsWithDeclinedRansomOffers.Contains(heroId)) ? 0.2f : 0.12f;
                if (MBRandom.RandomFloat < chanceToDisplayRansomOffer)
                {
                    float heroValue = new SetPrisonerFreeBarterable(hero, captorClanOfPrisoner.Leader, hero.PartyBelongedToAsPrisoner, ransomPayer).GetUnitValueForFaction(hero.Clan) * 1.1f;
                    if (heroValue > 1E-05f && ransomPayer.Gold + 1000 >= heroValue)
                    {
                        SetCurrentRansomHero(playerHeroId, hero, heroValue, ransomPayer);
                        network.SendAll(new NetworkRansomOfferSent(playerHeroId, heroId, heroValue));

                        return;
                    }
                }
            }
        }
        else if (!captorClanOfPrisoner.IsPlayerClan() && !hero.Clan.IsPlayerClan() && MBRandom.RandomFloat < 0.1f)
        {
            SetPrisonerFreeBarterable setPrisonerFreeBarterable = new(hero, captorClanOfPrisoner.Leader, hero.PartyBelongedToAsPrisoner, ransomPayer);
            if (setPrisonerFreeBarterable.GetValueForFaction(captorClanOfPrisoner) + setPrisonerFreeBarterable.GetValueForFaction(hero.Clan) > 0)
            {
                Campaign.Current.BarterManager.ExecuteAiBarter(captorClanOfPrisoner, hero.Clan, captorClanOfPrisoner.Leader, ransomPayer, setPrisonerFreeBarterable);
            }
        }
    }

    public void HandleDeclineRansomOffer(Hero victim)
    {
        foreach (var playerData in PlayerRansomOfferData)
        {
            if (playerData.Value.CurrentRansomHeroId == null) continue;

            if (!TryGetCurrentRansomHero(playerData.Key, out var currentRansomHero)) continue;
            if (!objectManager.TryGetObjectWithLogging<Hero>(playerData.Key, out var playerHero)) continue;

            if (victim == currentRansomHero || victim == playerHero)
            {
                DeclineRansomOffer(playerData.Key);
            }
        }
    }

    public void AcceptRansomOffer(string playerHeroId, string ransomHeroId)
    {
        if (!TryGetRansomOfferBehavior(out var ransomOfferBehavior)) return;
        if (!ValidatePlayerHeroId(playerHeroId, out var playerData, out var playerHero)) return;

        // The offer the player answered expired or was replaced before the response arrived
        if (playerData.CurrentRansomHeroId != ransomHeroId) return;

        if (!TryGetCurrentRansomHero(playerHeroId, out var currentRansomHero)) return;
        if (!TryGetCurrentRansomPayer(playerHeroId, out var currentRansomPayer)) return;

        var ransomPrice = (int)playerData.CurrentRansomPrice;

        // Vanilla only checks the player's gold when the inquiry opens, so it may have dropped since
        if (currentRansomPayer.IsPlayerHero() && currentRansomPayer.Gold < ransomPrice)
        {
            if (PlayerManager.TryGetControlledObjectInfo(currentRansomPayer, out var controller) &&
                playerManager.TryGetPeer(controller.ObjectControllerId, out var peer))
            {
                network.Send(peer, new SendInformationMessage(GameTexts.FindText("str_coop_ransom_no_longer_enough_gold").ToString()));

                // Resend the offer since accepting used up the notification, so the player can retry or decline it
                network.Send(peer, new NetworkRansomOfferSent(playerHeroId, playerData.CurrentRansomHeroId, playerData.CurrentRansomPrice));
            }
            return;
        }

        if (playerData.HeroIdsWithDeclinedRansomOffers.Contains(playerData.CurrentRansomHeroId))
        {
            playerData.HeroIdsWithDeclinedRansomOffers.Remove(playerData.CurrentRansomHeroId);
        }
        if (currentRansomPayer.Gold < ransomPrice + 1000 && !currentRansomPayer.IsPlayerHero())
        {
            currentRansomPayer.Gold = ransomPrice + 1000;
        }
        GiveGoldAction.ApplyBetweenCharacters(currentRansomPayer, ransomOfferBehavior.GetCaptorClanOfPrisoner(currentRansomHero).Leader, ransomPrice, false);
        EndCaptivityAction.ApplyByRansom(currentRansomHero, currentRansomHero.Clan.Leader);

        statisticsInterface.OnPlayerAcceptedRansomOffer(playerHero, ransomPrice);
    }

    public void DeclineRansomOffer(string playerHeroId, string playerRespondedRansomHeroId = null)
    {
        if (!ValidatePlayerHeroId(playerHeroId, out var playerData, out _)) return;

        // Skip a player's decline for an offer that expired or was replaced before the response arrived
        bool fromPlayer = playerRespondedRansomHeroId != null;
        if (fromPlayer && playerData.CurrentRansomHeroId != playerRespondedRansomHeroId) return;

        if (!TryGetCurrentRansomHero(playerHeroId, out var currentRansomHero)) return;

        // Vanilla fires this event on every DeclineRansomOffer call except for directly from player input.
        // Only publish the cancel message when coming from the server decline path.
        if (!fromPlayer)
        {
            network.SendAll(new NetworkRansomOfferCancelled(playerHeroId, playerData.CurrentRansomHeroId));
        } 

        if (currentRansomHero.IsPrisoner && currentRansomHero.IsAlive
            && !playerData.HeroIdsWithDeclinedRansomOffers.Contains(playerData.CurrentRansomHeroId))
        {
            playerData.HeroIdsWithDeclinedRansomOffers.Add(playerData.CurrentRansomHeroId);
        }
        SetCurrentRansomHero(playerHeroId, null, 0, null);
    }

    public void OnPrisonersChangeInSettlement(FlattenedTroopRoster roster, Hero prisoner, bool takenFromDungeon)
    {
        foreach (var playerData in PlayerRansomOfferData)
        {
            if (takenFromDungeon || playerData.Value.CurrentRansomHeroId == null) continue;

            if (!TryGetCurrentRansomHero(playerData.Key, out var currentRansomHero)) continue;

            if (prisoner == currentRansomHero)
            {
                DeclineRansomOffer(playerData.Key);
                continue;
            }
            if (roster == null) continue;

            foreach (FlattenedTroopRosterElement flattenedTroopRosterElement in roster)
            {
                if (flattenedTroopRosterElement.Troop.IsHero && flattenedTroopRosterElement.Troop.HeroObject == currentRansomHero)
                {
                    DeclineRansomOffer(playerData.Key);
                    break;
                }
            }
        }
    }

    public void HourlyTick()
    {
        foreach (var playerData in PlayerRansomOfferData)
        {
            var currentRansomOfferDate = new CampaignTime(playerData.Value.CurrentRansomOfferDateNumTicks);
            if (playerData.Value.CurrentRansomHeroId != null && currentRansomOfferDate.ElapsedDaysUntilNow >= 2f)
            {
                DeclineRansomOffer(playerData.Key);
            }
        }
    }

    public void AddNewPlayerHeroId(string playerHeroId, NetPeer peer)
    {
        if (PlayerRansomOfferData.TryGetValue(playerHeroId, out var playerData))
        {
            // The offer notification only lives on the client, so a rejoining player needs a pending offer sent again
            if (ModInformation.IsServer && peer != null && playerData.CurrentRansomHeroId != null)
            {
                network.Send(peer, new NetworkRansomOfferSent(playerHeroId, playerData.CurrentRansomHeroId, playerData.CurrentRansomPrice));
            }
            return;
        }

        PlayerRansomOfferData[playerHeroId] = new(new(), null, null, 0, 0);
    }

    public void SetClientRansomPrice(int ransomPrice)
    {
        clientRansomPrice = ransomPrice;
    }

    public int GetClientRansomPrice()
    {
        return clientRansomPrice;
    }

    private void SetCurrentRansomHero(string playerHeroId, Hero hero, float ransomValue, Hero ransomPayer = null)
    {
        if (!ValidatePlayerHeroId(playerHeroId, out var playerData, out _)) return;

        string heroId = null;
        if (hero != null && !objectManager.TryGetIdWithLogging(hero, out heroId)) return;

        string ransomPayerId = null;
        if (ransomPayer != null && !objectManager.TryGetIdWithLogging(ransomPayer, out ransomPayerId)) return;

        playerData.CurrentRansomHeroId = heroId;
        playerData.CurrentRansomPayerId = ransomPayerId;
        playerData.CurrentRansomOfferDateNumTicks = ((hero != null) ? CampaignTime.Now : CampaignTime.Never).NumTicks;
        playerData.CurrentRansomPrice = ransomValue;
    }

    private bool TryGetCurrentRansomHero(string playerHeroId, out Hero currentRansomHero)
    {
        return objectManager.TryGetObjectWithLogging(PlayerRansomOfferData[playerHeroId].CurrentRansomHeroId, out currentRansomHero);
    }

    private bool TryGetCurrentRansomPayer(string playerHeroId, out Hero currentRansomPayer)
    {
        return objectManager.TryGetObjectWithLogging(PlayerRansomOfferData[playerHeroId].CurrentRansomPayerId, out currentRansomPayer);
    }

    private bool ValidatePlayerHeroId(string playerHeroId, out RansomOfferData playerData, out Hero playerHero)
    {
        playerData = default;
        if (!objectManager.TryGetObjectWithLogging(playerHeroId, out playerHero)) return false;

        return PlayerRansomOfferData.TryGetValue(playerHeroId, out playerData);
    }

    private bool TryGetRansomOfferBehavior(out RansomOfferCampaignBehavior ransomOfferBehavior)
    {
        ransomOfferBehavior = Campaign.Current?.GetCampaignBehavior<RansomOfferCampaignBehavior>();
        if (ransomOfferBehavior != null) return true;

        Logger.Debug("Skipping ransom offer update because the campaign behavior is unavailable");
        return false;
    }
}
