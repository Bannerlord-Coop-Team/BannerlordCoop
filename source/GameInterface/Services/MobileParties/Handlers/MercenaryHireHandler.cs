using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Notifications.Messages;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Handlers;

/// <summary>
/// Replicates a tavern mercenary hire from a client to the server, which validates the synced
/// stock and applies the troop add and gold cost authoritatively so they reach every client.
/// </summary>
internal class MercenaryHireHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<MercenaryHireHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public MercenaryHireHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<MercenariesHired>(Handle_MercenariesHired);
        messageBroker.Subscribe<HireMercenaries>(Handle_HireMercenaries);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MercenariesHired>(Handle_MercenariesHired);
        messageBroker.Unsubscribe<HireMercenaries>(Handle_HireMercenaries);
    }

    internal void Handle_MercenariesHired(MessagePayload<MercenariesHired> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Town, out var townId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MercenaryTroop, out var mercenaryTroopId)) return;

        network.SendAll(new HireMercenaries(
            mainHeroId,
            mainPartyId,
            townId,
            mercenaryTroopId,
            obj.What.Count,
            obj.What.GoldAmount,
            obj.What.MainHero.Gold));
    }

    private void Handle_HireMercenaries(MessagePayload<HireMercenaries> obj)
    {
        // Only the server applies the hire authoritatively; clients receive the replicated troop and
        // gold changes instead.
        if (ModInformation.IsClient) return;

        var data = obj.What;
        var peer = obj.Who as NetPeer;

        // The hire applies vanilla game actions; defer them to the game-loop thread so they run there
        // instead of on the network (poller) thread that delivered the message.
        GameThread.RunSafe(() => ApplyHireMercenaries(data, peer), context: nameof(MercenaryHireHandler));
    }

    private void ApplyHireMercenaries(HireMercenaries data, NetPeer peer)
    {
        if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyId, out var mainParty)) return;
        if (!objectManager.TryGetObjectWithLogging<Town>(data.TownId, out var town)) return;
        if (!objectManager.TryGetObjectWithLogging<CharacterObject>(data.MercenaryTroopId, out var mercenaryTroop)) return;
        if (town.Settlement == null || !town.Settlement.IsTown) return;

        var recruitmentBehavior = Campaign.Current?.GetCampaignBehavior<RecruitmentCampaignBehavior>();
        var mercenaryData = recruitmentBehavior?.GetMercenaryData(town);
        int unitPrice = mercenaryData?.TroopType == null
            ? 0
            : Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(mercenaryData.TroopType, mainHero).RoundedResultNumber;
        int goldAmount = GetMercenaryHireGoldAmount(data.Count, unitPrice);
        bool availableTroopMatches = mercenaryData?.TroopType == mercenaryTroop;
        int availableCount = mercenaryData?.Number ?? 0;

        // The server stock may tick while a client is still in the tavern conversation. Reject those
        // stale requests against the current server stock, then publish the latest stock back below.
        if (mercenaryData == null ||
            !CanApplyMercenaryHire(
                data.Count,
                goldAmount,
                mainHero.Gold,
                unitPrice,
                availableTroopMatches,
                availableCount))
        {
            var availableTroopId = "unresolved";
            if (mercenaryData?.TroopType != null &&
                objectManager.TryGetIdWithLogging(mercenaryData.TroopType, out var resolvedAvailableTroopId))
            {
                availableTroopId = resolvedAvailableTroopId;
            }

            logger.Warning(
                "Rejected town mercenary hire for {TownId}: requested {RequestedTroopId} x{RequestedCount}, available {AvailableTroop} x{AvailableCount}, client hero gold {ClientHeroGold}, server hero gold {ServerHeroGold}, client cost {ClientGoldAmount}, server cost {GoldAmount}",
                data.TownId,
                data.MercenaryTroopId,
                data.Count,
                availableTroopId,
                availableCount,
                data.HeroGold,
                mainHero.Gold,
                data.GoldAmount,
                goldAmount);

            SendMercenaryStock(peer, town, mercenaryData?.TroopType, mercenaryData?.Number ?? 0);

            return;
        }

        // Apply with patches LIVE (no AllowedThread): the roster add replicates via the
        // TroopRoster patches and the gold change via the Hero.Gold sync.
        mainParty.AddElementToMemberRoster(mercenaryTroop, data.Count);
        GiveGoldAction.ApplyBetweenCharacters(mainHero, null, goldAmount, false);

        // The recruitment side effects of the hire. This is the host-safe equivalent of
        // vanilla CampaignEventDispatcher.OnUnitRecruited, whose listener reads Hero.MainHero /
        // MobileParty.MainParty (neither of which the dedicated host has); it runs against the
        // resolved hero/party instead, with patches live so the troop XP and the hero's
        // recruitment skill XP replicate to every client.
        if (mainHero.GetPerkValue(DefaultPerks.Leadership.FamousCommander))
        {
            mainParty.MemberRoster.AddXpToTroop(mercenaryTroop, (int)DefaultPerks.Leadership.FamousCommander.SecondaryBonus * data.Count);
        }
        SkillLevelingManager.OnTroopRecruited(mainHero, data.Count, mercenaryTroop.Tier);
        if (mercenaryTroop.Occupation == Occupation.Bandit)
        {
            SkillLevelingManager.OnBanditsRecruited(mainParty, mercenaryTroop, data.Count);
        }

        mercenaryData.ChangeMercenaryCount(-data.Count);
        RecruitmentCampaignBehaviorPatch.PublishMercenaryStock(recruitmentBehavior, town);
    }

    private void SendMercenaryStock(NetPeer peer, Town town, CharacterObject troopType, int number)
    {
        if (peer == null) return;
        if (!objectManager.TryGetIdWithLogging(town, out var townId)) return;

        string troopTypeId = null;
        if (troopType != null && !objectManager.TryGetId(troopType, out troopTypeId)) return;

        network.Send(peer, new NetworkUpdateMercenaryStock(townId, troopTypeId, number));
    }

    internal static int GetMercenaryHireGoldAmount(int count, int unitPrice)
    {
        if (count <= 0 || unitPrice <= 0)
            return 0;

        return count * unitPrice;
    }

    internal static bool CanApplyMercenaryHire(
        int count,
        int goldAmount,
        int serverHeroGold,
        int unitPrice,
        bool availableTroopMatches,
        int availableMercenaries)
    {
        return count > 0 &&
               goldAmount > 0 &&
               serverHeroGold >= goldAmount &&
               unitPrice > 0 &&
               availableTroopMatches &&
               availableMercenaries >= count;
    }
}
