using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.Transactions;
using Helpers;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Party.Handlers;

internal class PartyScreenHelperHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<PartyScreenHelperHandler>();
    private static readonly object DonationGate = new();
    private static readonly ConditionalWeakTable<NetPeer, PendingDonation>
        PendingDonations = new();
    private static readonly ConditionalWeakTable<NetPeer, PendingGarrisonDonation>
        PendingGarrisonDonations = new();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPlayerManager playerManager;

    public PartyScreenHelperHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<NewClanPartyScreenClosed>(Handle_NewClanPartyScreenClosed);
        messageBroker.Subscribe<CreateClanPartyAfterScreenClose>(Handle_CreateClanPartyAfterScreenClose);
        messageBroker.Subscribe<GarrisonDonated>(Handle_GarrisonDonated);
        messageBroker.Subscribe<DonateToGarrison>(Handle_DonateToGarrison);
        messageBroker.Subscribe<PrisonersDonated>(Handle_PrisonersDonated);
        messageBroker.Subscribe<DonatePrisoners>(Handle_DonatePrisoners);
        messageBroker.Subscribe<GarrisonManaged>(Handle_GarrisonManaged);
        messageBroker.Subscribe<DoManageGarrison>(Handle_DoManageGarrison);
        messageBroker.Subscribe<PrisonersReleasedAndTaken>(Handle_PrisonersReleasedAndTaken);
        messageBroker.Subscribe<ReleaseAndTakePrisoners>(Handle_ReleaseAndTakePrisoners);
        if (ModInformation.IsServer)
            ServerTransactionOutcome.Completed += HandleTransactionCompleted;
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NewClanPartyScreenClosed>(Handle_NewClanPartyScreenClosed);
        messageBroker.Unsubscribe<CreateClanPartyAfterScreenClose>(Handle_CreateClanPartyAfterScreenClose);
        messageBroker.Unsubscribe<GarrisonDonated>(Handle_GarrisonDonated);
        messageBroker.Unsubscribe<DonateToGarrison>(Handle_DonateToGarrison);
        messageBroker.Unsubscribe<PrisonersDonated>(Handle_PrisonersDonated);
        messageBroker.Unsubscribe<DonatePrisoners>(Handle_DonatePrisoners);
        messageBroker.Unsubscribe<GarrisonManaged>(Handle_GarrisonManaged);
        messageBroker.Unsubscribe<DoManageGarrison>(Handle_DoManageGarrison);
        messageBroker.Unsubscribe<PrisonersReleasedAndTaken>(Handle_PrisonersReleasedAndTaken);
        messageBroker.Unsubscribe<ReleaseAndTakePrisoners>(Handle_ReleaseAndTakePrisoners);
        if (ModInformation.IsServer)
            ServerTransactionOutcome.Completed -= HandleTransactionCompleted;
    }

    private static void HandleTransactionCompleted(
        NetPeer peer, int kind, bool success, string _)
    {
        if (kind != ServerTransactionOutcome.Party || success)
            return;
        ClearPendingPrisonerDonation(peer);
        ClearPendingGarrisonDonation(peer);
    }

    private void Handle_NewClanPartyScreenClosed(MessagePayload<NewClanPartyScreenClosed> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.NewLeaderHero, out var newLeaderHeroId)) return;

        var leftMemberRosterData = troopRosterInterface.PackTroopRosterData(obj.What.LeftMemberRoster);
        var leftPrisonRosterData = troopRosterInterface.PackTroopRosterData(obj.What.LeftPrisonRoster);

        var message = new CreateClanPartyAfterScreenClose(
            mainHeroId,
            newLeaderHeroId,
            leftMemberRosterData,
            leftPrisonRosterData
        );
        network.SendAll(message);
    }

    private void Handle_CreateClanPartyAfterScreenClose(MessagePayload<CreateClanPartyAfterScreenClose> obj)
    {
        NetPeer peer = obj.Who as NetPeer;
        GameThread.RunSafe(() => ServerTransactionOutcome.Execute(
            peer, ServerTransactionOutcome.ClanParty, () =>
        {
            string authenticationReason =
                "The server could not authenticate this player.";
            if (!playerManager.TryGetPlayer(peer, out var registeredPlayer) ||
                !ServerTransactionOutcome.TryResolvePlayer(
                    peer,
                    playerManager,
                    objectManager,
                    obj.What.MainHeroId,
                    registeredPlayer?.MobilePartyId,
                    out _,
                    out Hero mainHero,
                    out MobileParty sourceParty,
                    out authenticationReason))
            {
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    authenticationReason);
                return;
            }
            if (!objectManager.TryGetObjectWithLogging<Hero>(
                    obj.What.NewLeaderHeroId, out var newLeaderHero) ||
                newLeaderHero == null || newLeaderHero == mainHero ||
                newLeaderHero.Clan != mainHero.Clan ||
                newLeaderHero.PartyBelongedTo != sourceParty ||
                newLeaderHero.PartyBelongedToAsPrisoner != null ||
                newLeaderHero.IsChild ||
                !newLeaderHero.CanLeadParty() ||
                !newLeaderHero.CanBeGovernorOrHavePartyRole() ||
                newLeaderHero.GovernorOf != null ||
                newLeaderHero.HeroState != Hero.CharacterStates.Active ||
                sourceParty.MapEvent != null ||
                sourceParty.IsCurrentlyAtSea ||
                sourceParty.IsInRaftState ||
                mainHero.Clan.WarPartyComponents.Count >=
                    mainHero.Clan.WarPartyLimit)
            {
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    "The selected clan-party leader is no longer available.");
                return;
            }

            List<TroopRosterElement> members = troopRosterInterface
                .UnpackTroopRosterData(obj.What.LeftMemberRosterData)
                .ToList();
            List<TroopRosterElement> prisoners = troopRosterInterface
                .UnpackTroopRosterData(obj.What.LeftPrisonRosterData)
                .ToList();
            if (!ValidateClanPartyRoster(
                    sourceParty.MemberRoster,
                    members,
                    newLeaderHero.CharacterObject,
                    requireLeader: true) ||
                !ValidateClanPartyRoster(
                    sourceParty.PrisonRoster,
                    prisoners,
                    leader: null,
                    requireLeader: false))
            {
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    "The selected clan-party troops no longer match your party.");
                return;
            }

            int partyGoldLowerThreshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
            int requiredGold = Math.Max(
                0, partyGoldLowerThreshold - newLeaderHero.Gold);
            if (mainHero.Gold < requiredGold)
            {
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    "You no longer have enough denars to create that party.");
                return;
            }

            if (!TryBuildClanPartyTransferData(
                    members.Where(element =>
                        element.Character != newLeaderHero.CharacterObject),
                    out TroopRosterData memberRemove,
                    out TroopRosterData memberAdd) ||
                !TryBuildClanPartyTransferData(
                    prisoners,
                    out TroopRosterData prisonerRemove,
                    out TroopRosterData prisonerAdd))
            {
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty,
                    "A transferred troop could not be resolved.");
                return;
            }

            int leaderIndexBefore = sourceParty.MemberRoster.FindIndexOfTroop(
                newLeaderHero.CharacterObject);
            int leaderWoundedBefore = sourceParty.MemberRoster
                .GetElementWoundedNumber(leaderIndexBefore);
            int leaderXpBefore = sourceParty.MemberRoster
                .GetElementXp(leaderIndexBefore);
            int mainGoldBefore = mainHero.Gold;
            int leaderGoldBefore = newLeaderHero.Gold;
            MobileParty mobileParty = null;
            List<(TroopRoster roster, TroopRosterData delta)> deltas = null;
            bool transfersApplied = false;
            bool committed = false;
            string failure = "The clan party could not be created.";
            try
            {
                mobileParty = MobilePartyHelper.CreateNewClanMobileParty(
                    newLeaderHero, newLeaderHero.Clan);
                if (mobileParty == null)
                    throw new InvalidOperationException(failure);

                deltas = new List<(TroopRoster roster, TroopRosterData delta)>
                {
                    (sourceParty.MemberRoster, memberRemove),
                    (mobileParty.MemberRoster, memberAdd),
                    (sourceParty.PrisonRoster, prisonerRemove),
                    (mobileParty.PrisonRoster, prisonerAdd)
                };
                failure =
                    "The clan-party transfer changed before it could be committed.";
                if (!troopRosterInterface.TryApplyTroopRosterDeltas(deltas))
                    throw new InvalidOperationException(failure);
                transfersApplied = true;

                // Remote-player parties are not Bannerlord's global MainParty,
                // so vanilla may leave the leader in the source roster.
                int leaderIndex = sourceParty.MemberRoster.FindIndexOfTroop(
                    newLeaderHero.CharacterObject);
                if (leaderIndex >= 0)
                {
                    int wounded = sourceParty.MemberRoster
                        .GetElementWoundedNumber(leaderIndex);
                    int xp = sourceParty.MemberRoster.GetElementXp(leaderIndex);
                    sourceParty.MemberRoster.AddToCounts(
                        newLeaderHero.CharacterObject,
                        -1,
                        false,
                        -Math.Min(1, wounded),
                        -xp,
                        true,
                        -1);
                }
                if (requiredGold > 0)
                    GiveGoldAction.ApplyBetweenCharacters(
                        mainHero, newLeaderHero, requiredGold, false);
                committed = true;
            }
            catch (Exception exception)
            {
                logger.Error(
                    exception,
                    "Rolling back failed clan-party creation for {LeaderId}",
                    obj.What.NewLeaderHeroId);
            }

            if (!committed)
            {
                mainHero.Gold = mainGoldBefore;
                newLeaderHero.Gold = leaderGoldBefore;
                if (transfersApplied && deltas != null)
                    troopRosterInterface.TryApplyTroopRosterDeltas(
                        InvertRosterDeltas(deltas));
                if (mobileParty?.IsActive == true)
                    DestroyPartyAction.Apply(null, mobileParty);
                RestoreClanPartyLeader(
                    sourceParty,
                    newLeaderHero,
                    leaderWoundedBefore,
                    leaderXpBefore);
                ServerTransactionOutcome.Reject(
                    peer, ServerTransactionOutcome.ClanParty, failure);
                return;
            }

            try
            {
                network.Send(peer, new RefreshPartiesList());
            }
            catch (Exception exception)
            {
                logger.Warning(
                    exception,
                    "Clan party committed, but the party-list refresh could not be sent");
            }
            ServerTransactionOutcome.Accept(
                peer, ServerTransactionOutcome.ClanParty);
        }));
    }

    private bool ValidateClanPartyRoster(
        TroopRoster source,
        IReadOnlyCollection<TroopRosterElement> requested,
        CharacterObject leader,
        bool requireLeader)
    {
        if (source == null || requested == null)
            return false;
        var seen = new HashSet<CharacterObject>();
        int leaderCount = 0;
        foreach (TroopRosterElement element in requested)
        {
            if (element.Character == null || element.Number <= 0 ||
                element.WoundedNumber < 0 ||
                element.WoundedNumber > element.Number || element.Xp < 0 ||
                !seen.Add(element.Character))
                return false;
            int index = source.FindIndexOfTroop(element.Character);
            if (index < 0 || source.GetElementNumber(index) < element.Number ||
                source.GetElementWoundedNumber(index) < element.WoundedNumber ||
                source.GetElementXp(index) < element.Xp ||
                source.GetElementNumber(index) - element.Number == 0 &&
                source.GetElementXp(index) - element.Xp != 0)
                return false;
            if (element.Character == leader)
                leaderCount += element.Number;
        }
        return !requireLeader || leaderCount == 1;
    }

    private bool TryBuildClanPartyTransferData(
        IEnumerable<TroopRosterElement> requested,
        out TroopRosterData removeData,
        out TroopRosterData addData)
    {
        var remove = new List<TroopRosterElementData>();
        var add = new List<TroopRosterElementData>();
        removeData = default;
        addData = default;
        foreach (TroopRosterElement element in requested)
        {
            if (!objectManager.TryGetId(element.Character, out string id))
                return false;
            remove.Add(new TroopRosterElementData(
                id, -element.Number, -element.WoundedNumber, -element.Xp));
            add.Add(new TroopRosterElementData(
                id, element.Number, element.WoundedNumber, element.Xp));
        }
        removeData = new TroopRosterData(remove);
        addData = new TroopRosterData(add);
        return true;
    }

    private static List<(TroopRoster roster, TroopRosterData delta)>
        InvertRosterDeltas(
            IEnumerable<(TroopRoster roster, TroopRosterData delta)> deltas)
    {
        return deltas.Select(entry => (
            entry.roster,
            new TroopRosterData((entry.delta.Data ??
                Array.Empty<TroopRosterElementData>())
                .Select(element => new TroopRosterElementData(
                    element.CharacterId,
                    -element.Number,
                    -element.WoundedNumber,
                    -element.Xp)))))
            .ToList();
    }

    private static void RestoreClanPartyLeader(
        MobileParty sourceParty,
        Hero leader,
        int wounded,
        int xp)
    {
        if (sourceParty == null || leader?.CharacterObject == null ||
            sourceParty.MemberRoster.Contains(leader.CharacterObject))
            return;
        sourceParty.MemberRoster.AddToCounts(
            leader.CharacterObject,
            1,
            false,
            Math.Min(1, Math.Max(0, wounded)),
            Math.Max(0, xp),
            true,
            -1);
    }

    private void Handle_GarrisonDonated(MessagePayload<GarrisonDonated> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.CurrentSettlement, out var currentSettlementId)) return;
        var troops = new List<DonateTroop>();
        for (int i = 0; i < obj.What.LeftMemberRoster.Count; i++)
        {
            TroopRosterElement element =
                obj.What.LeftMemberRoster.GetElementCopyAtIndex(i);
            if (element.Number <= 0 ||
                !objectManager.TryGetIdWithLogging(
                    element.Character, out string characterId))
                continue;
            troops.Add(new DonateTroop(characterId, element.Number));
        }

        var message = new DonateToGarrison(currentSettlementId, troops);
        network.SendAll(message);
    }

    private void Handle_DonateToGarrison(MessagePayload<DonateToGarrison> obj)
    {
        if (ModInformation.IsServer && obj.Who is NetPeer peer)
        {
            lock (DonationGate)
            {
                PendingGarrisonDonations.Remove(peer);
                PendingGarrisonDonations.Add(
                    peer,
                    new PendingGarrisonDonation(obj.What, DateTime.UtcNow));
            }
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.CurrentSettlementId, out var currentSettlement)) return;

        GameThread.RunSafe(() =>
        {
            MobileParty garrisonParty = currentSettlement.Town.GarrisonParty;
            if (garrisonParty == null)
            {
                currentSettlement.AddGarrisonParty();
                garrisonParty = currentSettlement.Town.GarrisonParty;
            }
            foreach (DonateTroop troop in obj.What.Troops)
            {
                if (troop.Count <= 0 ||
                    !objectManager.TryGetObjectWithLogging(
                        troop.CharacterId, out CharacterObject character))
                    continue;
                garrisonParty.AddElementToMemberRoster(
                    character, troop.Count, false);
                if (character.IsHero)
                {
                    EnterSettlementAction.ApplyForCharacterOnly(
                        character.HeroObject, currentSettlement);
                }
            }
        });
    }

    private void Handle_PrisonersDonated(MessagePayload<PrisonersDonated> obj)
    {
        FlattenedTroop[] rightSidePrisonerRoster = FlattenedTroopSerializer.Serialize(obj.What.RightSidePrisonerRoster, objectManager);
        if (!objectManager.TryGetIdWithLogging(obj.What.CurrentSettlement, out var currentSettlementId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.RightParty, out var rightPartyId)) return;

        var message = new DonatePrisoners(rightSidePrisonerRoster, currentSettlementId, rightPartyId);
        network.SendAll(message);
    }

    private void Handle_DonatePrisoners(MessagePayload<DonatePrisoners> obj)
    {
        if (ModInformation.IsServer && obj.Who is NetPeer peer)
        {
            lock (DonationGate)
            {
                PendingDonations.Remove(peer);
                PendingDonations.Add(
                    peer,
                    new PendingDonation(obj.What, DateTime.UtcNow));
            }
            return;
        }

        FlattenedTroopRoster rightSidePrisonerRoster = FlattenedTroopSerializer.Deserialize(obj.What.RightSidePrisonerRoster, objectManager);
        if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.CurrentSettlementId, out var currentSettlement)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.RightPartyId, out var rightParty)) return;

        GameThread.RunSafe(() =>
        {
            foreach (CharacterObject characterObject in rightSidePrisonerRoster.Troops)
            {
                if (characterObject.IsHero)
                {
                    EnterSettlementAction.ApplyForPrisoner(characterObject.HeroObject, currentSettlement);
                }
            }
            CampaignEventDispatcher.Instance.OnPrisonerDonatedToSettlement(rightParty.MobileParty, rightSidePrisonerRoster, currentSettlement);
        });
    }

    internal static bool TryGetPendingPrisonerDonation(
        NetPeer peer,
        out DonatePrisoners donation)
    {
        donation = default;
        if (peer == null)
            return false;
        lock (DonationGate)
        {
            if (!PendingDonations.TryGetValue(peer, out PendingDonation pending))
                return false;
            if (DateTime.UtcNow - pending.CreatedUtc > TimeSpan.FromSeconds(30))
            {
                PendingDonations.Remove(peer);
                return false;
            }
            donation = pending.Message;
            return true;
        }
    }

    internal static void ClearPendingPrisonerDonation(NetPeer peer)
    {
        if (peer == null)
            return;
        lock (DonationGate)
            PendingDonations.Remove(peer);
    }

    internal static bool TryGetPendingGarrisonDonation(
        NetPeer peer,
        out DonateToGarrison donation)
    {
        donation = default;
        if (peer == null)
            return false;
        lock (DonationGate)
        {
            if (!PendingGarrisonDonations.TryGetValue(
                    peer, out PendingGarrisonDonation pending))
                return false;
            if (DateTime.UtcNow - pending.CreatedUtc > TimeSpan.FromSeconds(30))
            {
                PendingGarrisonDonations.Remove(peer);
                return false;
            }
            donation = pending.Message;
            return true;
        }
    }

    internal static void ClearPendingGarrisonDonation(NetPeer peer)
    {
        if (peer == null)
            return;
        lock (DonationGate)
            PendingGarrisonDonations.Remove(peer);
    }

    private sealed class PendingDonation
    {
        internal readonly DonatePrisoners Message;
        internal readonly DateTime CreatedUtc;

        internal PendingDonation(DonatePrisoners message, DateTime createdUtc)
        {
            Message = message;
            CreatedUtc = createdUtc;
        }
    }

    private sealed class PendingGarrisonDonation
    {
        internal readonly DonateToGarrison Message;
        internal readonly DateTime CreatedUtc;

        internal PendingGarrisonDonation(
            DonateToGarrison message,
            DateTime createdUtc)
        {
            Message = message;
            CreatedUtc = createdUtc;
        }
    }

    private void Handle_GarrisonManaged(MessagePayload<GarrisonManaged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.CurrentSettlement, out var currentSettlementId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.LeftMemberRoster, out var leftMemberRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.LeftPrisonerRoster, out var leftPrisonerRosterId)) return;

        var message = new DoManageGarrison(currentSettlementId, leftMemberRosterId, leftPrisonerRosterId);
        network.SendAll(message);
    }

    private void Handle_DoManageGarrison(MessagePayload<DoManageGarrison> obj)
    {
        if (ModInformation.IsServer) return;

        if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.CurrentSettlementId, out var currentSettlement)) return;
        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(obj.What.LeftMemberRosterId, out var leftMemberRoster)) return;
        if (!objectManager.TryGetObjectWithLogging<TroopRoster>(obj.What.LeftPrisonerRosterId, out var leftPrisonerRoster)) return;

        GameThread.RunSafe(() =>
        {
            for (int i = 0; i < leftMemberRoster.Count; i++)
            {
                TroopRosterElement elementCopyAtIndex = leftMemberRoster.GetElementCopyAtIndex(i);
                if (elementCopyAtIndex.Character.IsHero)
                {
                    EnterSettlementAction.ApplyForCharacterOnly(elementCopyAtIndex.Character.HeroObject, currentSettlement);
                }
            }
            for (int j = 0; j < leftPrisonerRoster.Count; j++)
            {
                TroopRosterElement elementCopyAtIndex2 = leftPrisonerRoster.GetElementCopyAtIndex(j);
                if (elementCopyAtIndex2.Character.IsHero)
                {
                    EnterSettlementAction.ApplyForPrisoner(elementCopyAtIndex2.Character.HeroObject, currentSettlement);
                }
            }
        });
    }

    private void Handle_PrisonersReleasedAndTaken(MessagePayload<PrisonersReleasedAndTaken> obj)
    {
        FlattenedTroop[] takenPrisonerRoster = FlattenedTroopSerializer.Serialize(obj.What.TakenPrisonerRoster, objectManager);
        FlattenedTroop[] releasedPrisonerRoster = FlattenedTroopSerializer.Serialize(obj.What.ReleasedPrisonerRoster, objectManager);

        var message = new ReleaseAndTakePrisoners(takenPrisonerRoster, releasedPrisonerRoster);
        network.SendAll(message);
    }

    private void Handle_ReleaseAndTakePrisoners(MessagePayload<ReleaseAndTakePrisoners> obj)
    {
        if (ModInformation.IsServer) return;

        FlattenedTroopRoster takenPrisonerRoster = FlattenedTroopSerializer.Deserialize(obj.What.TakenPrisonerRoster, objectManager);
        FlattenedTroopRoster releasedPrisonerRoster = FlattenedTroopSerializer.Deserialize(obj.What.ReleasedPrisonerRoster, objectManager);

        GameThread.RunSafe(() =>
        {
            releasedPrisonerRoster = FilterPlayerPrisonerReleases(releasedPrisonerRoster);
            if (!releasedPrisonerRoster.IsEmpty<FlattenedTroopRosterElement>())
            {
                EndCaptivityAction.ApplyByReleasedByChoice(releasedPrisonerRoster);
            }
            if (!takenPrisonerRoster.IsEmpty<FlattenedTroopRosterElement>())
            {
                TakePrisonerAction.ApplyByTakenFromPartyScreen(takenPrisonerRoster);
            }
        });
    }

    private FlattenedTroopRoster FilterPlayerPrisonerReleases(FlattenedTroopRoster releasedPrisonerRoster)
    {
        var nonPlayerRoster = new FlattenedTroopRoster(4);

        foreach (var element in releasedPrisonerRoster)
        {
            var hero = element.Troop?.HeroObject;
            if (hero != null && hero.IsPlayerHero())
            {
                continue;
            }

            nonPlayerRoster[element.Descriptor] = element;
        }

        return nonPlayerRoster;
    }
}
