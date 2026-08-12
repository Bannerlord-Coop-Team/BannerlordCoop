using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Companions.Handlers;

internal class HireCompanionHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<HireCompanionHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly LocationConversationTracker locationConversationTracker;

    public HireCompanionHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        LocationConversationTracker locationConversationTracker)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.locationConversationTracker = locationConversationTracker;

        messageBroker.Subscribe<CompanionHired>(Handle_CompanionHired);
        messageBroker.Subscribe<HireCompanion>(Handle_HireCompanion);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CompanionHired>(Handle_CompanionHired);
        messageBroker.Unsubscribe<HireCompanion>(Handle_HireCompanion);
    }

    private void Handle_CompanionHired(MessagePayload<CompanionHired> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero, out var oneToOneConversationHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.PlayerClan, out var playerClanId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;

        var message = new HireCompanion(
            mainHeroId,
            oneToOneConversationHeroId,
            obj.What.HiringPrice,
            playerClanId,
            mainPartyId
        );

        network.SendAll(message);
    }

    private void Handle_HireCompanion(MessagePayload<HireCompanion> obj)
    {
        if (!ModInformation.IsServer || obj == null ||
            !(obj.Who is NetPeer peer))
            return;

        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryResolveAuthoritativeRequest(
                    peer,
                    data.OneToOneConversationHeroId,
                    out var mainHero,
                    out var playerClan,
                    out var mainParty,
                    out var companion,
                    out var price,
                    out var reason))
            {
                Reject(peer, reason);
                return;
            }

            if (companion.CompanionOf == playerClan &&
                companion.PartyBelongedTo == mainParty)
                return;

            int mainGoldBefore = mainHero.Gold;
            int companionGoldBefore = companion.Gold;
            int memberCountBefore =
                mainParty.MemberRoster.GetTroopRoster()
                    .Where(x => x.Character == companion.CharacterObject)
                    .Sum(x => x.Number);
            Clan companionOfBefore = companion.CompanionOf;
            var stayingBefore = companion.StayingInSettlement;

            try
            {
                // Preserve Bannerlord's native event ordering: payment first,
                // then clan membership, then the party-joined event. Listeners
                // must never observe a joined wanderer whose CompanionOf is null.
                GiveGoldAction.ApplyBetweenCharacters(
                    mainHero, companion, price, false);
                AddCompanionAction.Apply(playerClan, companion);
                AddHeroToPartyAction.Apply(companion, mainParty, true);

                if (mainHero.Gold != mainGoldBefore - price ||
                    companion.CompanionOf != playerClan ||
                    companion.PartyBelongedTo != mainParty)
                {
                    throw new InvalidOperationException(
                        "The authoritative companion state did not commit.");
                }
            }
            catch (Exception exception)
            {
                mainHero.Gold = mainGoldBefore;
                companion.Gold = companionGoldBefore;
                companion.CompanionOf = companionOfBefore;
                int memberCountNow =
                    mainParty.MemberRoster.GetTroopRoster()
                        .Where(x => x.Character == companion.CharacterObject)
                        .Sum(x => x.Number);
                int rosterDelta = memberCountBefore - memberCountNow;
                if (rosterDelta != 0)
                    mainParty.MemberRoster.AddToCounts(
                        companion.CharacterObject, rosterDelta);
                companion.StayingInSettlement = stayingBefore;

                logger.Error(
                    exception,
                    "Authoritative companion hire failed for {CompanionId}",
                    data.OneToOneConversationHeroId);
                Reject(peer, "The companion could not be hired safely. Please try again.");
            }
        });
    }

    private bool TryResolveAuthoritativeRequest(
        NetPeer peer,
        string companionId,
        out Hero mainHero,
        out Clan playerClan,
        out MobileParty mainParty,
        out Hero companion,
        out int price,
        out string reason)
    {
        mainHero = null;
        playerClan = null;
        mainParty = null;
        companion = null;
        price = 0;
        reason = "The companion request could not be authenticated.";

        if (!playerManager.TryGetPlayer(peer, out var player) ||
            player == null ||
            !objectManager.TryGetObject(player.HeroId, out mainHero) ||
            !objectManager.TryGetObject(player.ClanId, out playerClan) ||
            !objectManager.TryGetObject(player.MobilePartyId, out mainParty) ||
            mainParty.LeaderHero != mainHero ||
            mainHero.Clan != playerClan)
            return false;

        if (string.IsNullOrEmpty(companionId) ||
            !objectManager.TryGetObject(companionId, out companion) ||
            companion == null)
        {
            reason = "That companion is no longer available.";
            return false;
        }

        if (companion.CompanionOf == playerClan &&
            companion.PartyBelongedTo == mainParty)
            return true;

        if (!companion.IsAlive || !companion.IsWanderer ||
            companion.CompanionOf != null || companion.IsPrisoner ||
            companion.PartyBelongedTo != null)
        {
            reason = "That wanderer is no longer available for hire.";
            return false;
        }

        var settlement = mainParty.CurrentSettlement;
        if (settlement == null || companion.CurrentSettlement != settlement ||
            mainHero.CurrentSettlement != settlement)
        {
            reason = "You must speak to this companion in the same settlement.";
            return false;
        }

        Hero resolvedCompanion = companion;
        var companionLocations = settlement.LocationComplex?.GetListOfLocations()
            .Where(location => location.GetCharacterList()?.Any(entry =>
                entry?.Character == resolvedCompanion.CharacterObject ||
                entry?.Character?.HeroObject == resolvedCompanion) == true)
            .ToArray();
        if (companionLocations?.Length != 1 ||
            !objectManager.TryGetId(companion.CharacterObject, out var characterId) ||
            !objectManager.TryGetId(companionLocations[0], out var locationId) ||
            !locationConversationTracker.TryGetEngagement(peer, out var npcKey) ||
            !string.Equals(
                npcKey,
                LocationConversationTracker.ComposeKey(locationId, characterId),
                StringComparison.Ordinal))
        {
            reason = "The companion conversation is no longer active.";
            return false;
        }

        if (playerClan.Companions.Count() >= playerClan.CompanionLimit)
        {
            reason = "Your clan cannot support another companion.";
            return false;
        }

        if (mainParty.Party.NumberOfAllMembers >= mainParty.Party.PartySizeLimit)
        {
            reason = "Your party has no room for another companion.";
            return false;
        }

        try
        {
            price = GetAuthoritativeHiringPrice(
                mainHero, mainParty, companion);
        }
        catch (Exception exception)
        {
            logger.Error(exception,
                "Could not calculate authoritative companion hire price for {CompanionId}",
                companionId);
            reason = "The server could not calculate this companion's hiring price.";
            return false;
        }
        if (price < 0 || mainHero.Gold < price)
        {
            reason = "You do not have enough gold to hire this companion.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reproduces Bannerlord 1.4.8's default companion-price model with the
    /// authenticated player in place of Hero.MainHero/MobileParty.MainParty.
    /// Those client singletons are null on a dedicated host.
    /// </summary>
    private static int GetAuthoritativeHiringPrice(
        Hero mainHero,
        MobileParty mainParty,
        Hero companion)
    {
        var result = new TaleWorlds.CampaignSystem.ExplainedNumber(
            0f, includeDescriptions: false, null);
        Town town = companion.CurrentSettlement?.Town ??
            SettlementHelper.FindNearestTownToMobileParty(
                mainParty, MobileParty.NavigationType.All);
        if (town == null)
            throw new InvalidOperationException(
                "No authoritative market was available for companion pricing.");

        float equipmentValue = 0f;
        for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot;
             slot < EquipmentIndex.NumEquipmentSetSlots;
             slot++)
        {
            EquipmentElement battle = companion.CharacterObject.Equipment[slot];
            if (battle.Item != null)
                equipmentValue += town.GetItemPrice(battle);
            EquipmentElement civilian =
                companion.CharacterObject.FirstCivilianEquipment[slot];
            if (civilian.Item != null)
                equipmentValue += town.GetItemPrice(civilian);
        }

        result.Add(equipmentValue / 2f);
        result.Add(companion.CharacterObject.Level * 10);
        if (mainHero.IsPartyLeader &&
            mainHero.GetPerkValue(DefaultPerks.Steward.PaidInPromise))
        {
            result.AddFactor(
                DefaultPerks.Steward.PaidInPromise.PrimaryBonus);
        }
        if (mainParty != null)
        {
            PerkHelper.AddPerkBonusForParty(
                DefaultPerks.Trade.GreatInvestor,
                mainParty,
                isPrimaryBonus: false,
                ref result);
        }
        return (int)result.ResultNumber;
    }

    private void Reject(NetPeer peer, string reason)
    {
        if (peer == null) return;
        network.Send(peer, new SendInformationMessage(
            string.IsNullOrWhiteSpace(reason)
                ? "The companion could not be hired."
                : reason));
    }
}
