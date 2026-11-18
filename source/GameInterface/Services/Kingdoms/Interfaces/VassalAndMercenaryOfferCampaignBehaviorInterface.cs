using Common;
using Common.Logging;
using Common.Network;
using GameInterface.Configuration;
using GameInterface.CoopSessionData;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using Helpers;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace GameInterface.Services.Kingdoms.Interfaces;

public interface IVassalAndMercenaryOfferCampaignBehaviorInterface : IGameAbstraction
{
    void DailyTick();
    void OnHeroPrisonerTaken(Hero prisoner);
    void OnClanChangedKingdom(Clan clan, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail);
    void OnWarDeclared(IFaction faction1, IFaction faction2);
    void OnHeroRelationChanged(Hero effectiveHero, Hero effectiveHeroGainedRelationWith);
    void OnKingdomDestroyed(Kingdom destroyedKingdom);
    void AcceptMercenaryOffer(string playerHeroId, string kingdomId, NetPeer peer);
    void DeclineOffer(string playerHeroId, string kingdomId);
    // Withdraws only the kingdom's vassal invitation to the player.
    void CancelVassalOffer(string playerHeroId, string kingdomId);
    // Withdraws the offers a kingdom still has out to players it can no longer take under maxClansPerKingdom.
    void WithdrawOffersIfFull(Kingdom kingdom);
    void AddNewPlayerHeroId(string playerHeroId, NetPeer peer);
}

public class VassalAndMercenaryOfferCampaignBehaviorInterface : IVassalAndMercenaryOfferCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<VassalAndMercenaryOfferCampaignBehaviorInterface>();

    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IKingdomClanLimit kingdomClanLimit;

    private Dictionary<string, VassalAndMercenaryOfferData> PlayerVassalAndMercenaryOfferData => coopSessionProvider.CoopSession.InteractionsPlayerData.PlayerVassalAndMercenaryOfferData;

    public VassalAndMercenaryOfferCampaignBehaviorInterface(
        ICoopSessionProvider coopSessionProvider,
        IObjectManager objectManager,
        INetwork network,
        IKingdomClanLimit kingdomClanLimit)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.network = network;
        this.kingdomClanLimit = kingdomClanLimit;
    }

    public void DailyTick()
    {
        if (!TryGetVassalAndMercenaryOfferBehavior(out var behavior)) return;

        foreach (var playerData in PlayerVassalAndMercenaryOfferData)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(playerData.Key, out var playerHero)) continue;

            // Offers are made to the clan, so only its leader gets them
            Clan clan = playerHero.Clan;
            if (clan?.Leader != playerHero) continue;
            if (playerData.Value.StopOffers || clan.Tier <= Campaign.Current.Models.ClanTierModel.MinClanTier) continue;

            // Vanilla offer conditions read Hero.MainHero and Clan.PlayerClan
            using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
            {
                if (playerData.Value.CurrentMercenaryOfferKingdomId != null)
                {
                    var offerDate = new CampaignTime(playerData.Value.CurrentMercenaryOfferDateNumTicks);
                    CancelMercenaryOfferIfInvalid(behavior, playerData.Key, playerData.Value, clan, offerDate.ElapsedDaysUntilNow >= VassalAndMercenaryOfferCampaignBehavior.MercenaryOfferCancelTimeInDays);
                    continue;
                }

                if (playerHero.IsPrisoner || playerHero.PartyBelongedTo?.IsInRaftState != false) continue;

                float randomFloat = MBRandom.RandomFloat;
                if (randomFloat <= VassalAndMercenaryOfferCampaignBehavior.MercenaryOfferCreationChance && behavior.CanPlayerClanReceiveMercenaryOffer())
                {
                    Kingdom kingdom = Kingdom.All.GetRandomElementWithPredicate(kingdom =>
                        behavior.MercenaryKingdomSelectionConditionsHold(kingdom) &&
                        !kingdomClanLimit.IsFull(clan, kingdom, asMercenary: true, ModConfigProvider.ModOptions));
                    if (kingdom != null)
                    {
                        CreateMercenaryOffer(playerData.Key, playerData.Value, kingdom);
                    }
                }
                else if (randomFloat <= VassalAndMercenaryOfferCampaignBehavior.VassalOfferCreationChance && behavior.CanPlayerClanReceiveVassalOffer())
                {
                    Kingdom kingdom = Kingdom.All.GetRandomElementWithPredicate(kingdom => VassalKingdomSelectionConditionsHold(playerData.Value, clan, kingdom));
                    if (kingdom != null)
                    {
                        CreateVassalOffer(playerData.Key, playerData.Value, kingdom);
                    }
                }
            }
        }
    }

    public void OnHeroPrisonerTaken(Hero prisoner)
    {
        foreach (var playerData in PlayerVassalAndMercenaryOfferData)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(playerData.Key, out var playerHero)) continue;

            if (prisoner == playerHero && playerData.Value.CurrentMercenaryOfferKingdomId != null)
            {
                CancelAllOffers(playerData.Key, playerData.Value);
                continue;
            }

            if (prisoner.IsKingdomLeader && prisoner.MapFaction is Kingdom kingdom)
            {
                CancelOffer(playerData.Key, playerData.Value, kingdom);
            }
        }
    }

    public void OnClanChangedKingdom(Clan clan, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail)
    {
        if (newKingdom == null) return;
        WithdrawOffersIfFull(newKingdom);
        if (!clan.IsPlayerClan()) return;
        if (!objectManager.TryGetIdWithLogging(newKingdom, out var newKingdomId)) return;

        foreach (var playerData in PlayerVassalAndMercenaryOfferData)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(playerData.Key, out var playerHero)) continue;
            if (playerHero.Clan != clan) continue;

            string mercenaryOfferKingdomId = playerData.Value.CurrentMercenaryOfferKingdomId;
            if (detail == ChangeKingdomAction.ChangeKingdomActionDetail.JoinAsMercenary && mercenaryOfferKingdomId != null && mercenaryOfferKingdomId != newKingdomId)
            {
                CancelOffer(playerData.Key, playerData.Value, mercenaryOfferKingdomId);
            }
            else if (detail == ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom || detail == ChangeKingdomAction.ChangeKingdomActionDetail.CreateKingdom)
            {
                playerData.Value.StopOffers = true;
                CancelAllOffers(playerData.Key, playerData.Value);
            }
        }
    }

    public void OnWarDeclared(IFaction faction1, IFaction faction2)
    {
        if (!TryGetVassalAndMercenaryOfferBehavior(out var behavior)) return;

        foreach (var playerData in PlayerVassalAndMercenaryOfferData)
        {
            if (playerData.Value.CurrentMercenaryOfferKingdomId == null) continue;
            if (!objectManager.TryGetObjectWithLogging<Hero>(playerData.Key, out var playerHero)) continue;

            if (faction1 == playerHero.Clan || faction2 == playerHero.Clan)
            {
                using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
                {
                    CancelMercenaryOfferIfInvalid(behavior, playerData.Key, playerData.Value, playerHero.Clan);
                }
            }
        }
    }

    public void OnHeroRelationChanged(Hero effectiveHero, Hero effectiveHeroGainedRelationWith)
    {
        if (!TryGetVassalAndMercenaryOfferBehavior(out var behavior)) return;

        foreach (var playerData in PlayerVassalAndMercenaryOfferData)
        {
            if (playerData.Value.CurrentMercenaryOfferKingdomId == null) continue;
            if (!objectManager.TryGetObjectWithLogging<Hero>(playerData.Key, out var playerHero)) continue;

            if (effectiveHero == playerHero || effectiveHeroGainedRelationWith == playerHero)
            {
                using (new MainHeroSubstitutionScope(playerHero, playerHero.PartyBelongedTo))
                {
                    CancelMercenaryOfferIfInvalid(behavior, playerData.Key, playerData.Value, playerHero.Clan);
                }
            }
        }
    }

    public void OnKingdomDestroyed(Kingdom destroyedKingdom)
    {
        foreach (var playerData in PlayerVassalAndMercenaryOfferData)
        {
            CancelOffer(playerData.Key, playerData.Value, destroyedKingdom);
        }
    }

    public void AcceptMercenaryOffer(string playerHeroId, string kingdomId, NetPeer peer)
    {
        if (!ValidatePlayerHeroId(playerHeroId, out var playerData, out var playerHero)) return;

        // The offer the player answered expired or was cancelled before the response arrived. A click already on its
        // way when the kingdom took its last clan still gets the reason.
        if (playerData.CurrentMercenaryOfferKingdomId != kingdomId)
        {
            if (objectManager.TryGetObject<Kingdom>(kingdomId, out var withdrawnKingdom) &&
                kingdomClanLimit.IsFull(playerHero.Clan, withdrawnKingdom, asMercenary: true, ModConfigProvider.ModOptions))
                network.Send(peer, new NetworkKingdomFull(kingdomId));
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<Kingdom>(kingdomId, out var kingdom)) return;

        ClearKingdomOffer(playerData, kingdomId);

        Clan clan = playerHero.Clan;
        if (clan.Kingdom != null)
        {
            Logger.Warning("Rejected mercenary offer because clan {ClanId} already belongs to a kingdom", clan.StringId);
            return;
        }

        if (kingdomClanLimit.IsFull(clan, kingdom, asMercenary: true, ModConfigProvider.ModOptions))
        {
            Logger.Warning("Rejected mercenary offer for clan {ClanId} because kingdom {KingdomId} has maxClansPerKingdom clans", clan.StringId, kingdomId);
            network.Send(peer, new NetworkKingdomFull(kingdomId));
            return;
        }

        int mercenaryAwardFactorToJoinKingdom = Campaign.Current.Models.MinorFactionsModel.GetMercenaryAwardFactorToJoinKingdom(clan, kingdom, true);
        ChangeKingdomAction.ApplyByJoinFactionAsMercenary(clan, kingdom, default, mercenaryAwardFactorToJoinKingdom);
    }

    public void DeclineOffer(string playerHeroId, string kingdomId)
    {
        if (!ValidatePlayerHeroId(playerHeroId, out var playerData, out _)) return;

        ClearKingdomOffer(playerData, kingdomId);
    }

    public void CancelVassalOffer(string playerHeroId, string kingdomId)
    {
        if (!PlayerVassalAndMercenaryOfferData.TryGetValue(playerHeroId, out var playerData)) return;

        WithdrawOffer(playerHeroId, playerData, kingdomId, mercenary: false);
    }

    public void WithdrawOffersIfFull(Kingdom kingdom)
    {
        if (!objectManager.TryGetIdWithLogging(kingdom, out var kingdomId)) return;

        foreach (var playerData in PlayerVassalAndMercenaryOfferData)
            WithdrawFullOffers(playerData.Key, playerData.Value, kingdom, kingdomId);
    }

    private void WithdrawFullOffers(string playerHeroId, VassalAndMercenaryOfferData playerData, Kingdom kingdom, string kingdomId)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(playerHeroId, out var playerHero)) return;

        if (playerData.CurrentMercenaryOfferKingdomId == kingdomId &&
            kingdomClanLimit.IsFull(playerHero.Clan, kingdom, asMercenary: true, ModConfigProvider.ModOptions))
            WithdrawOffer(playerHeroId, playerData, kingdomId, mercenary: true);
        if (playerData.VassalOffers.ContainsKey(kingdomId) &&
            kingdomClanLimit.IsFull(playerHero.Clan, kingdom, asMercenary: false, ModConfigProvider.ModOptions))
            WithdrawOffer(playerHeroId, playerData, kingdomId, mercenary: false);
    }

    // The client's cancel clears every offer from the kingdom, so it only goes out once none is left.
    private void WithdrawOffer(string playerHeroId, VassalAndMercenaryOfferData playerData, string kingdomId, bool mercenary)
    {
        if (mercenary)
        {
            if (playerData.CurrentMercenaryOfferKingdomId != kingdomId) return;
            playerData.CurrentMercenaryOfferKingdomId = null;
            playerData.CurrentMercenaryOfferDateNumTicks = 0;
        }
        else if (!playerData.VassalOffers.Remove(kingdomId))
        {
            return;
        }

        if (playerData.CurrentMercenaryOfferKingdomId == kingdomId || playerData.VassalOffers.ContainsKey(kingdomId)) return;
        network.SendAll(new NetworkVassalOrMercenaryOfferCancelled(playerHeroId, kingdomId));
    }

    public void AddNewPlayerHeroId(string playerHeroId, NetPeer peer)
    {
        if (PlayerVassalAndMercenaryOfferData.TryGetValue(playerHeroId, out var playerData))
        {
            // Offer notifications only live on the client, so a rejoining player needs pending offers sent again
            if (ModInformation.IsServer && peer != null)
            {
                // A kingdom that filled while the player was away can't take them, so its offers aren't sent again.
                var offeredKingdomIds = playerData.VassalOffers.Keys.Append(playerData.CurrentMercenaryOfferKingdomId)
                    .Where(id => id != null).Distinct().ToList();
                foreach (var offeredKingdomId in offeredKingdomIds)
                {
                    if (objectManager.TryGetObjectWithLogging<Kingdom>(offeredKingdomId, out var offeredKingdom))
                        WithdrawFullOffers(playerHeroId, playerData, offeredKingdom, offeredKingdomId);
                }

                if (playerData.CurrentMercenaryOfferKingdomId != null)
                {
                    network.Send(peer, new NetworkMercenaryOfferSent(playerHeroId, playerData.CurrentMercenaryOfferKingdomId));
                }

                foreach (var vassalOfferKingdomId in playerData.VassalOffers.Keys)
                {
                    network.Send(peer, new NetworkVassalOfferSent(playerHeroId, vassalOfferKingdomId));
                }
            }
            return;
        }

        PlayerVassalAndMercenaryOfferData[playerHeroId] = new(null, 0, new(), false);
    }

    private bool VassalKingdomSelectionConditionsHold(VassalAndMercenaryOfferData playerData, Clan clan, Kingdom kingdom)
    {
        if (!objectManager.TryGetIdWithLogging(kingdom, out var kingdomId)) return false;

        return !playerData.VassalOffers.ContainsKey(kingdomId)
            && FactionHelper.CanPlayerOfferVassalage(kingdom, out _, out _)
            && !kingdomClanLimit.IsFull(clan, kingdom, asMercenary: false, ModConfigProvider.ModOptions)
            && !kingdom.Leader.IsPrisoner
            && !kingdom.Leader.IsFugitive;
    }

    private void CancelMercenaryOfferIfInvalid(VassalAndMercenaryOfferCampaignBehavior behavior, string playerHeroId, VassalAndMercenaryOfferData playerData, Clan clan, bool expired = false)
    {
        // Cancel on a failed lookup too, otherwise DailyTick never creates another offer for this player.
        if (!objectManager.TryGetObjectWithLogging<Kingdom>(playerData.CurrentMercenaryOfferKingdomId, out var kingdom) ||
            expired || !behavior.MercenaryKingdomSelectionConditionsHold(kingdom) ||
            kingdomClanLimit.IsFull(clan, kingdom, asMercenary: true, ModConfigProvider.ModOptions))
        {
            CancelOffer(playerHeroId, playerData, playerData.CurrentMercenaryOfferKingdomId);
        }
    }

    private void CreateMercenaryOffer(string playerHeroId, VassalAndMercenaryOfferData playerData, Kingdom kingdom)
    {
        if (!objectManager.TryGetIdWithLogging(kingdom, out var kingdomId)) return;

        playerData.CurrentMercenaryOfferKingdomId = kingdomId;
        playerData.CurrentMercenaryOfferDateNumTicks = CampaignTime.Now.NumTicks;

        network.SendAll(new NetworkMercenaryOfferSent(playerHeroId, kingdomId));
    }

    private void CreateVassalOffer(string playerHeroId, VassalAndMercenaryOfferData playerData, Kingdom kingdom)
    {
        if (!objectManager.TryGetIdWithLogging(kingdom, out var kingdomId)) return;

        playerData.VassalOffers[kingdomId] = CampaignTime.Now.NumTicks;

        network.SendAll(new NetworkVassalOfferSent(playerHeroId, kingdomId));
    }

    private void CancelAllOffers(string playerHeroId, VassalAndMercenaryOfferData playerData)
    {
        if (playerData.CurrentMercenaryOfferKingdomId != null)
        {
            CancelOffer(playerHeroId, playerData, playerData.CurrentMercenaryOfferKingdomId);
        }

        foreach (var vassalOfferKingdomId in playerData.VassalOffers.Keys.ToList())
        {
            CancelOffer(playerHeroId, playerData, vassalOfferKingdomId);
        }
    }

    private void CancelOffer(string playerHeroId, VassalAndMercenaryOfferData playerData, Kingdom kingdom)
    {
        if (!objectManager.TryGetIdWithLogging(kingdom, out var kingdomId)) return;

        CancelOffer(playerHeroId, playerData, kingdomId);
    }

    private void CancelOffer(string playerHeroId, VassalAndMercenaryOfferData playerData, string kingdomId)
    {
        // Vanilla fires the cancel event even without an offer, only tell the client when there was one to remove
        if (!ClearKingdomOffer(playerData, kingdomId)) return;

        network.SendAll(new NetworkVassalOrMercenaryOfferCancelled(playerHeroId, kingdomId));
    }

    private bool ClearKingdomOffer(VassalAndMercenaryOfferData playerData, string kingdomId)
    {
        bool cleared = playerData.VassalOffers.Remove(kingdomId);

        if (playerData.CurrentMercenaryOfferKingdomId == kingdomId)
        {
            playerData.CurrentMercenaryOfferKingdomId = null;
            playerData.CurrentMercenaryOfferDateNumTicks = 0;
            cleared = true;
        }

        return cleared;
    }

    private bool ValidatePlayerHeroId(string playerHeroId, out VassalAndMercenaryOfferData playerData, out Hero playerHero)
    {
        playerData = default;
        if (!objectManager.TryGetObjectWithLogging(playerHeroId, out playerHero)) return false;

        return PlayerVassalAndMercenaryOfferData.TryGetValue(playerHeroId, out playerData);
    }

    private bool TryGetVassalAndMercenaryOfferBehavior(out VassalAndMercenaryOfferCampaignBehavior behavior)
    {
        behavior = Campaign.Current?.GetCampaignBehavior<VassalAndMercenaryOfferCampaignBehavior>();
        if (behavior != null) return true;

        Logger.Debug("Skipping vassal and mercenary offer update because the campaign behavior is unavailable");
        return false;
    }
}
