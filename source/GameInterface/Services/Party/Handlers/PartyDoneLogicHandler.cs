using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.TroopRosters.Messages;
using GameInterface.Services.Transactions;
using GameInterface.Services.UI.Notifications.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Party.Handlers;

internal class PartyDoneLogicHandler : IHandler
{
    private const string PartyChangedMessage =
        "The party changed before these edits were applied. Reopen the party screen and try again.";

    private static readonly ILogger logger = LogManager.GetLogger<PartyDoneLogicHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPlayerManager playerManager;
    private readonly IBattlePartyGrantRegistry battlePartyGrants;

    public PartyDoneLogicHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager,
        IBattlePartyGrantRegistry battlePartyGrants)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;
        this.battlePartyGrants = battlePartyGrants;

        messageBroker.Subscribe<PartyDoneLogicAttempted>(Handle_PartyDoneLogicAttempted);
        messageBroker.Subscribe<NetworkCompleteDoneLogic>(Handle_CompletePartyDoneLogic);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyDoneLogicAttempted>(Handle_PartyDoneLogicAttempted);
        messageBroker.Unsubscribe<NetworkCompleteDoneLogic>(Handle_CompletePartyDoneLogic);
    }

    // Client
    private void Handle_PartyDoneLogicAttempted(MessagePayload<PartyDoneLogicAttempted> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        string leftPartyId = null;
        if (obj.What.LeftParty != null && 
            !objectManager.TryGetIdWithLogging(obj.What.LeftParty, out leftPartyId))
            return;

        // Not registered when donating
        objectManager.TryGetId(obj.What.LeftPrisonerRoster, out var leftPrisonerRosterId);

        var upgradedTroopHistory = new UpgradedTroopHistoryData(new());
        foreach (Tuple<CharacterObject, CharacterObject, int> tuple in obj.What.UpgradedTroopHistory)
        {
            if (!objectManager.TryGetIdWithLogging(tuple.Item1, out var character1Id)) continue;
            if (!objectManager.TryGetIdWithLogging(tuple.Item2, out var character2Id)) continue;

            upgradedTroopHistory.Data.Add(new(character1Id, character2Id, tuple.Item3));
        }

        // Send only the per-troop change the player made (current minus the screen-open snapshot). Heroes and
        // companions that did not change net to zero and are omitted, so the server needs no special handling
        // for them when re-applying the delta.
        var leftMemberRosterData = troopRosterInterface.PackTroopRosterDelta(obj.What.LeftMemberRoster, obj.What.InitialLeftMemberRoster);
        var leftPrisonerRosterData = troopRosterInterface.PackTroopRosterDelta(obj.What.LeftPrisonerRoster, obj.What.InitialLeftPrisonerRoster);
        var rightMemberRosterData = troopRosterInterface.PackTroopRosterDelta(obj.What.RightMemberRoster, obj.What.InitialRightMemberRoster);
        var rightPrisonerRosterData = troopRosterInterface.PackTroopRosterDelta(obj.What.RightPrisonerRoster, obj.What.InitialRightPrisonerRoster);

        var rightMemberOrderData = troopRosterInterface.PackTroopRosterOrderData(obj.What.RightMemberRoster);

        var releaserPartyPosition = GetReleaserPartyPosition(obj.What.MainHero);

        var message = new NetworkCompleteDoneLogic(
            mainHeroId,
            FlattenedTroopSerializer.Serialize(obj.What.ReleasedPrisonersRoster, objectManager),
            FlattenedTroopSerializer.Serialize(obj.What.TakenPrisonersRoster, objectManager),
            FlattenedTroopSerializer.Serialize(obj.What.RecruitedPrisonersRoster, objectManager),
            leftMemberRosterData,
            leftPrisonerRosterData,
            rightMemberRosterData,
            rightPrisonerRosterData,
            // ItemRoster._data is the backing-capacity array and contains
            // default padding rows. Send only the logical roster contents so
            // normal party upgrades/prisoner commits are not rejected as an
            // invalid inventory snapshot.
            obj.What.RightOwnerPartyItemRoster.ToArray(),
            upgradedTroopHistory,
            leftPartyId,
            leftPrisonerRosterId,
            obj.What.PartyGoldChangeAmount,
            obj.What.PartyInfluenceChangeAmount,
            obj.What.PartyMoraleChangeAmount,
            obj.What.DoNotApplyGoldTransactions,
            releaserPartyPosition,
            obj.What.PartyScreenMode,
            rightMemberOrderData,
            obj.What.ApplyReleasedAndTakenPrisonerActions
        );

        network.SendAll(message);
    }

    private static CampaignVec2 GetReleaserPartyPosition(Hero mainHero)
    {
        var releaserParty = mainHero.PartyBelongedTo;
        if (releaserParty?.CurrentSettlement != null)
            return releaserParty.CurrentSettlement.GatePosition;

        if (releaserParty != null)
            return releaserParty.Position;

        return MobileParty.MainParty.Position;
    }

    // Server
    private void Handle_CompletePartyDoneLogic(MessagePayload<NetworkCompleteDoneLogic> obj)
    {
        var message = obj.What;
        var requester = obj.Who as NetPeer;
        
        GameThread.RunSafe(() => ServerTransactionOutcome.Execute(
            requester, ServerTransactionOutcome.Party, () =>
        {
            string authenticationReason =
                "The server could not authenticate this player.";
            if (!playerManager.TryGetPlayer(requester, out var registeredPlayer) ||
                !ServerTransactionOutcome.TryResolvePlayer(
                    requester,
                    playerManager,
                    objectManager,
                    message.MainHeroId,
                    registeredPlayer?.MobilePartyId,
                    out _,
                    out Hero mainHero,
                    out MobileParty playerParty,
                    out authenticationReason))
            {
                ServerTransactionOutcome.Reject(
                    requester, ServerTransactionOutcome.Party,
                    authenticationReason);
                return;
            }

            if (!TryResolveCompleteDoneLogic(
                    message, out var leftParty, out var leftPrisonerRoster,
                    out var upgradedTroopHistory))
            {
                ServerTransactionOutcome.Reject(
                    requester, ServerTransactionOutcome.Party,
                    "The other party is no longer available.");
                return;
            }
            TroopRoster questRoster = null;
            int? questGoldChange = null;
            if (message.PartyScreenMode ==
                    Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage &&
                !TryResolveQuestTroopSource(
                    mainHero,
                    message,
                    upgradedTroopHistory,
                    out questRoster,
                    out questGoldChange,
                    out string questReason))
            {
                ServerTransactionOutcome.Reject(
                    requester,
                    ServerTransactionOutcome.Party,
                    questReason);
                return;
            }
            BattlePartyClaim battlePartyClaim = null;
            if (leftParty == null && leftPrisonerRoster == null &&
                questRoster == null &&
                message.PartyScreenMode ==
                    Helpers.PartyScreenHelper.PartyScreenMode.Loot &&
                message.ApplyReleasedAndTakenPrisonerActions)
            {
                BattlePartyClaimStatus claimStatus =
                    battlePartyGrants.TryPrepareClaim(
                        registeredPlayer.ControllerId,
                        registeredPlayer.HeroId,
                        registeredPlayer.MobilePartyId,
                        message.LeftMemberRosterData,
                        message.LeftPrisonerRosterData,
                        out battlePartyClaim,
                        out string claimReason);
                if (claimStatus == BattlePartyClaimStatus.Rejected)
                {
                    ServerTransactionOutcome.Reject(
                        requester,
                        ServerTransactionOutcome.Party,
                        claimReason);
                    return;
                }
            }
            if (!IsAuthorizedPartySource(
                    mainHero,
                    playerParty,
                    leftParty,
                    leftPrisonerRoster,
                    questRoster,
                    battlePartyClaim,
                    message,
                    out bool allowBattleRescue))
            {
                ServerTransactionOutcome.Reject(
                    requester, ServerTransactionOutcome.Party,
                    "The other party is not available to this player.");
                return;
            }

            var releasedPrisonersRoster = FlattenedTroopSerializer.Deserialize(message.ReleasedPrisonersRoster, objectManager);
            var takenPrisonersRoster = FlattenedTroopSerializer.Deserialize(message.TakenPrisonersRoster, objectManager);
            var recruitedPrisonersRoster = FlattenedTroopSerializer.Deserialize(message.RecruitedPrisonersRoster, objectManager);
            bool hasPrisonerDonation =
                PartyScreenHelperHandler.TryGetPendingPrisonerDonation(
                    requester,
                    out DonatePrisoners pendingDonation);
            if (hasPrisonerDonation && !TryValidatePrisonerDonation(
                    pendingDonation,
                    playerParty,
                    releasedPrisonersRoster,
                    message.RightPrisonerRosterData,
                    out string donationReason))
            {
                ServerTransactionOutcome.Reject(
                    requester,
                    ServerTransactionOutcome.Party,
                    donationReason);
                return;
            }
            bool hasGarrisonDonation =
                PartyScreenHelperHandler.TryGetPendingGarrisonDonation(
                    requester,
                    out DonateToGarrison pendingGarrisonDonation);
            MobileParty donationGarrison = null;
            Settlement donationSettlement = null;
            bool createdDonationGarrison = false;
            if (hasGarrisonDonation && !TryValidateGarrisonDonation(
                    pendingGarrisonDonation,
                    playerParty,
                    message,
                    out donationSettlement,
                    out donationGarrison,
                    out createdDonationGarrison,
                    out string garrisonDonationReason))
            {
                ServerTransactionOutcome.Reject(
                    requester,
                    ServerTransactionOutcome.Party,
                    garrisonDonationReason);
                return;
            }
            bool isRansom = message.PartyScreenMode ==
                Helpers.PartyScreenHelper.PartyScreenMode.Ransom;
            TroopRoster ransomPrisoners = null;
            if (isRansom && !TryBuildAuthoritativeRansomSale(
                    playerParty,
                    message,
                    upgradedTroopHistory,
                    out ransomPrisoners,
                    out string ransomReason))
            {
                RollbackCreatedDonationGarrison(
                    donationGarrison, createdDonationGarrison);
                logger.Warning(
                    "Rejected ransom sale for {MainHeroId}: {Reason}",
                    message.MainHeroId,
                    ransomReason);
                ServerTransactionOutcome.Reject(
                    requester,
                    ServerTransactionOutcome.Party,
                    ransomReason);
                return;
            }
            var releasedPlayerCaptivityEvents = new List<PlayerCaptivityEndedByServer>();
            var leftPrisonerRosterData = message.LeftPrisonerRosterData;
            var rightPrisonerRosterData = message.RightPrisonerRosterData;
            // Validate the client-reported release/take history against the signed delta before
            // removing player-prisoner releases from the apply delta. Player releases are handled
            // by PlayerCaptivityServerHandler rather than by a roster mutation, so validating the
            // filtered delta would always reject a legitimate dismissal (the -1 entry is gone).
            var signedRightPrisonerRosterData = rightPrisonerRosterData;
            // SellPrisonersHandler owns ransom releases so the same player is not released twice.
            if (!hasPrisonerDonation &&
                message.PartyScreenMode != Helpers.PartyScreenHelper.PartyScreenMode.Ransom)
            {
                releasedPlayerCaptivityEvents = CreatePlayerCaptivityReleaseEvents(
                    message.LeftPrisonerRosterData,
                    message.RightPrisonerRosterData,
                    HasLeftPrisonerTransferDestination(
                        message.ApplyReleasedAndTakenPrisonerActions,
                        leftParty != null,
                        leftPrisonerRoster != null),
                    message.ReleaserPartyPosition,
                    out leftPrisonerRosterData,
                    out rightPrisonerRosterData);
            }
            var takenHeroCharacterIds = new HashSet<string>();
            bool validatePrisonerActions =
                message.ApplyReleasedAndTakenPrisonerActions ||
                hasPrisonerDonation;
            var actionRostersAreValid =
                !validatePrisonerActions ||
                TryValidatePrisonerActionRosters(
                    releasedPrisonersRoster,
                    takenPrisonersRoster,
                    signedRightPrisonerRosterData,
                    out takenHeroCharacterIds);
            if (!actionRostersAreValid)
            {
                RollbackCreatedDonationGarrison(
                    donationGarrison, createdDonationGarrison);
                logger.Error("Rejected Party screen prisoner actions because transfer history did not match the signed right-prisoner delta");
                ServerTransactionOutcome.Reject(
                    requester, ServerTransactionOutcome.Party,
                    "Prisoner changes no longer match the server state.");
                return;
            }
            if (message.ApplyReleasedAndTakenPrisonerActions)
            {
                rightPrisonerRosterData = FilterTakenHeroAdditions(
                    rightPrisonerRosterData,
                    takenHeroCharacterIds);
            }

            var rosterDeltas = CreateRosterDeltas(
                mainHero,
                leftParty,
                leftPrisonerRoster,
                questRoster,
                message,
                leftPrisonerRosterData,
                rightPrisonerRosterData);
            if (hasGarrisonDonation)
                rosterDeltas.Add((
                    donationGarrison.MemberRoster,
                    message.LeftMemberRosterData));

            if (!TryValidatePartyCommit(
                    mainHero,
                    message,
                    upgradedTroopHistory,
                    rosterDeltas,
                    hasPrisonerDonation,
                    hasGarrisonDonation,
                    questGoldChange,
                    battlePartyClaim == null
                        ? Array.Empty<TroopRosterData>()
                        : new[]
                        {
                            battlePartyClaim.MemberSourceDelta,
                            battlePartyClaim.PrisonerSourceDelta
                        },
                    out string commitReason))
            {
                RollbackCreatedDonationGarrison(
                    donationGarrison, createdDonationGarrison);
                ServerTransactionOutcome.Reject(
                    requester,
                    ServerTransactionOutcome.Party,
                    commitReason);
                return;
            }
            if (!TryValidateRosterRoleChanges(
                    playerParty,
                    recruitedPrisonersRoster,
                    takenPrisonersRoster,
                    upgradedTroopHistory,
                    message.LeftMemberRosterData,
                    message.LeftPrisonerRosterData,
                    message.RightMemberRosterData,
                    signedRightPrisonerRosterData,
                    allowBattleRescue,
                    battlePartyClaim))
            {
                RollbackCreatedDonationGarrison(
                    donationGarrison, createdDonationGarrison);
                ServerTransactionOutcome.Reject(
                    requester, ServerTransactionOutcome.Party,
                    "Recruited prisoners no longer match the party changes.");
                return;
            }

            if (!battlePartyGrants.TryActivate(battlePartyClaim))
            {
                ServerTransactionOutcome.Reject(
                    requester,
                    ServerTransactionOutcome.Party,
                    "Your post-battle party award changed before it could be committed.");
                return;
            }

            // Commit the authoritative rosters, inventory and gold as one reversible core.
            // Later campaign notifications are effects of that committed state and must never
            // turn a completed action into a retryable rejection.
            ItemRosterElement[] itemRosterBefore =
                mainHero.PartyBelongedTo.ItemRoster.ToArray();
            var prisonerRosterBefore =
                playerParty.PrisonRoster.GetTroopRoster();
            int goldBefore = mainHero.Gold;
            bool rostersApplied = false;
            try
            {
                // SellPrisonersAction owns ransom roster changes.
                if (!isRansom)
                {
                    if (!troopRosterInterface.TryApplyTroopRosterDeltas(
                            rosterDeltas))
                        throw new InvalidOperationException(PartyChangedMessage);
                    rostersApplied = true;
                }
                // The ransom screen cannot mutate inventory. Retain the live
                // authoritative roster instead of overwriting it with the
                // screen-open client snapshot while food consumption continues.
                if (!isRansom)
                    ApplyRightOwnerPartyItemRoster(mainHero, message);
                ApplyPartyRewardChanges(
                    mainHero,
                    message,
                    suppressInfluence:
                        hasPrisonerDonation || hasGarrisonDonation);
                if (isRansom)
                    SellPrisonersHandler.ApplySale(
                        playerParty, ransomPrisoners);
                if (!battlePartyGrants.Consume(battlePartyClaim))
                    throw new InvalidOperationException(
                        "The server post-battle party award changed during its commit.");
                battlePartyClaim = null;
            }
            catch (Exception exception)
            {
                battlePartyGrants.Release(battlePartyClaim);
                mainHero.Gold = goldBefore;
                // Ransom mutates the prisoner roster outside rosterDeltas. Normal party
                // operations are rolled back by the inverse deltas below; restoring both
                // would apply the prisoner rollback twice.
                if (isRansom)
                {
                    playerParty.PrisonRoster.Clear();
                    foreach (TroopRosterElement element in prisonerRosterBefore)
                        playerParty.PrisonRoster.AddToCounts(
                            element.Character,
                            element.Number,
                            false,
                            element.WoundedNumber,
                            element.Xp,
                            true);
                }
                mainHero.PartyBelongedTo.ItemRoster.Clear();
                mainHero.PartyBelongedTo.ItemRoster.Add(itemRosterBefore);
                bool rolledBack = !rostersApplied ||
                    troopRosterInterface.TryApplyTroopRosterDeltas(
                        InvertRosterDeltas(rosterDeltas));
                if (rolledBack)
                    RollbackCreatedDonationGarrison(
                        donationGarrison, createdDonationGarrison);
                logger.Error(
                    exception,
                    "Rolled back rejected party commit for {MainHeroId}; rosterRollback={RosterRollback}",
                    message.MainHeroId,
                    rolledBack);
                if (requester != null)
                    network.Send(
                        requester,
                        new SendInformationMessage(PartyChangedMessage));
                ServerTransactionOutcome.Reject(
                    requester, ServerTransactionOutcome.Party,
                    PartyChangedMessage);
                return;
            }

            // The state commit above is final. Consume one-shot donation state before
            // any throwable notification so it cannot contaminate the next action.
            if (hasPrisonerDonation)
                PartyScreenHelperHandler.ClearPendingPrisonerDonation(requester);
            if (hasGarrisonDonation)
                PartyScreenHelperHandler.ClearPendingGarrisonDonation(requester);
            if (isRansom)
                SellPrisonersHandler.ClearPendingSale(requester);

            // Each post-commit effect is isolated. A stale hero/reference must not
            // prevent unrelated rewards and progression from being applied.
            TryPostPartyEffect(
                () => PublishPlayerCaptivityReleaseEvents(releasedPlayerCaptivityEvents),
                message.MainHeroId,
                "captivity release events");
            if (message.ApplyReleasedAndTakenPrisonerActions)
                TryPostPartyEffect(
                    () => ApplyReleasedAndTakenPrisonerActions(
                        mainHero, releasedPrisonersRoster, takenPrisonersRoster),
                    message.MainHeroId,
                    "prisoner actions");
            TryPostPartyEffect(
                () => NotifyTakenPrisonersChanged(takenPrisonersRoster),
                message.MainHeroId,
                "prisoner settlement notification");
            if (hasPrisonerDonation)
                TryPostPartyEffect(
                    () => ApplyPrisonerDonation(
                        playerParty,
                        releasedPrisonersRoster,
                        playerParty.CurrentSettlement),
                    message.MainHeroId,
                    "prisoner donation rewards");
            if (hasGarrisonDonation)
                TryPostPartyEffect(
                    () => ApplyGarrisonDonation(
                        mainHero,
                        donationSettlement,
                        message.LeftMemberRosterData),
                    message.MainHeroId,
                    "garrison donation rewards");
            TryPostPartyEffect(
                () => ApplyUpgradedTroopHistory(mainHero, upgradedTroopHistory),
                message.MainHeroId,
                "troop upgrade effects");
            TryPostPartyEffect(
                () => ApplyPrisonerRecruitmentEffects(
                    mainHero, message, recruitedPrisonersRoster),
                message.MainHeroId,
                "prisoner recruitment effects");
            TryPostPartyEffect(
                () => ApplyRosterOrder(
                    mainHero.PartyBelongedTo.MemberRoster,
                    message.RightMemberOrderData),
                message.MainHeroId,
                "party roster order");
            if (isRansom)
                TryPostPartyEffect(
                    () => SellPrisonersHandler.NotifySaleCommitted(
                        requester, playerParty),
                    message.MainHeroId,
                    "ransom roster refresh");
            ServerTransactionOutcome.Accept(
                requester, ServerTransactionOutcome.Party);
        }));
    }

    private bool TryResolveQuestTroopSource(
        Hero mainHero,
        NetworkCompleteDoneLogic message,
        IReadOnlyCollection<Tuple<CharacterObject, CharacterObject, int>> upgrades,
        out TroopRoster questRoster,
        out int? expectedGoldChange,
        out string reason)
    {
        questRoster = null;
        expectedGoldChange = null;
        reason = "The quest troop selection no longer matches the server state.";

        // OpenScreenAsQuest uses an ownerless left roster, so its authoritative
        // identity is the active IssueBase.AlternativeSolutionSentTroops roster.
        // Never accept a client-created dummy roster as the source of troops.
        if (mainHero?.Clan == null ||
            !string.IsNullOrEmpty(message.LeftPartyId) ||
            !string.IsNullOrEmpty(message.LeftPrisonerRosterId) ||
            upgrades?.Count > 0 ||
            HasAnyDelta(message.LeftPrisonerRosterData) ||
            HasAnyDelta(message.RightPrisonerRosterData) ||
            (message.ReleasedPrisonersRoster?.Length ?? 0) != 0 ||
            (message.TakenPrisonersRoster?.Length ?? 0) != 0 ||
            (message.RecruitedPrisonersRoster?.Length ?? 0) != 0 ||
            message.ApplyReleasedAndTakenPrisonerActions ||
            message.DoNotApplyGoldTransactions ||
            message.PartyInfluenceChangeAmount != 0 ||
            message.PartyMoraleChangeAmount != 0)
            return false;

        if (!TryReadQuestTransferDeltas(
                message.LeftMemberRosterData,
                message.RightMemberRosterData,
                out Dictionary<CharacterObject, TroopRosterElementData> transfers))
            return false;

        IssueBase matchedIssue = null;
        TroopRoster matchedRoster = null;
        int matchedGold = 0;
        var issues = Campaign.Current?.IssueManager?.Issues;
        if (issues == null)
            return false;

        foreach (IssueBase issue in issues.Values)
        {
            Hero companion = issue?.AlternativeSolutionHero;
            if (issue == null || !issue.IsOngoingWithoutQuest ||
                !issue.IsThereAlternativeSolution || companion == null ||
                companion.Clan != mainHero.Clan ||
                issue.AlternativeSolutionSentTroops == null)
                continue;

            if (!TryBuildQuestRosterAndGold(
                    issue, transfers, out TroopRoster proposed,
                    out int proposedGold))
                continue;

            // The old wire has no issue ID. Ambiguity is therefore rejected
            // instead of guessing and moving troops into another active issue.
            if (matchedIssue != null)
                return false;
            matchedIssue = issue;
            matchedRoster = issue.AlternativeSolutionSentTroops;
            matchedGold = proposedGold;
        }

        if (matchedIssue == null)
            return false;
        questRoster = matchedRoster;
        expectedGoldChange = matchedGold;
        return true;
    }

    private bool TryReadQuestTransferDeltas(
        TroopRosterData leftData,
        TroopRosterData rightData,
        out Dictionary<CharacterObject, TroopRosterElementData> transfers)
    {
        transfers = new Dictionary<CharacterObject, TroopRosterElementData>();
        var right = new Dictionary<CharacterObject, TroopRosterElementData>();
        foreach (TroopRosterElementData element in
                 leftData.Data ?? Array.Empty<TroopRosterElementData>())
        {
            if (!objectManager.TryGetObject(
                    element.CharacterId, out CharacterObject character) ||
                character == null || character.IsHero ||
                character.IsNotTransferableInPartyScreen ||
                element.Number == 0 ||
                element.Number > 0 &&
                    (element.WoundedNumber < 0 ||
                     element.WoundedNumber > element.Number || element.Xp < 0) ||
                element.Number < 0 &&
                    (element.WoundedNumber > 0 ||
                     element.WoundedNumber < element.Number || element.Xp > 0) ||
                transfers.ContainsKey(character))
                return false;
            transfers.Add(character, element);
        }
        foreach (TroopRosterElementData element in
                 rightData.Data ?? Array.Empty<TroopRosterElementData>())
        {
            if (!objectManager.TryGetObject(
                    element.CharacterId, out CharacterObject character) ||
                character == null || right.ContainsKey(character))
                return false;
            right.Add(character, element);
        }

        return transfers.Count > 0 && transfers.Count == right.Count &&
            transfers.All(pair =>
                right.TryGetValue(pair.Key, out TroopRosterElementData other) &&
                other.Number == -pair.Value.Number &&
                other.WoundedNumber == -pair.Value.WoundedNumber &&
                other.Xp == -pair.Value.Xp);
    }

    private static bool TryBuildQuestRosterAndGold(
        IssueBase issue,
        IReadOnlyDictionary<CharacterObject, TroopRosterElementData> transfers,
        out TroopRoster proposed,
        out int expectedGold)
    {
        proposed = TroopRoster.CreateDummyTroopRoster();
        expectedGold = 0;
        try
        {
            var current = new Dictionary<CharacterObject,
                (int number, int wounded, int xp)>();
            foreach (TroopRosterElement element in
                     issue.AlternativeSolutionSentTroops.GetTroopRoster())
            {
                current[element.Character] = (
                    element.Number, element.WoundedNumber, element.Xp);
                proposed.AddToCounts(
                    element.Character,
                    element.Number,
                    false,
                    element.WoundedNumber,
                    element.Xp,
                    true);
            }

            foreach (var transfer in transfers)
            {
                if (!issue.IsTroopTypeNeededByAlternativeSolution(transfer.Key))
                    return false;
                TroopRosterElementData delta = transfer.Value;
                current.TryGetValue(transfer.Key, out var before);
                long finalNumber = (long)before.number + delta.Number;
                long finalWounded = (long)before.wounded + delta.WoundedNumber;
                long finalXp = (long)before.xp + delta.Xp;
                if (finalNumber < 0 || finalNumber > int.MaxValue ||
                    finalWounded < 0 || finalWounded > finalNumber ||
                    finalXp < 0 || finalXp > int.MaxValue ||
                    finalNumber == 0 && finalXp != 0)
                    return false;
                proposed.AddToCounts(
                    transfer.Key,
                    delta.Number,
                    false,
                    delta.WoundedNumber,
                    delta.Xp,
                    true);
            }

            int needed = issue.GetTotalAlternativeSolutionNeededMenCount();
            if (needed <= 1 || proposed.TotalManCount > needed + 1 ||
                proposed.TotalRegulars < needed ||
                proposed.TotalRegulars - proposed.TotalWoundedRegulars < needed ||
                !issue.DoTroopsSatisfyAlternativeSolution(proposed, out _))
                return false;

            int days = issue.GetTotalAlternativeSolutionDurationInDays();
            if (days < 0)
                return false;
            long cost = 0;
            foreach (TroopRosterElement element in proposed.GetTroopRoster())
            {
                cost += (long)element.Character.TroopWage *
                    element.Number * days;
                if (cost > int.MaxValue)
                    return false;
            }
            expectedGold = -(int)cost;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasAnyDelta(TroopRosterData data)
        => (data.Data ?? Array.Empty<TroopRosterElementData>()).Any(element =>
            element.Number != 0 || element.WoundedNumber != 0 ||
            element.Xp != 0);

    private bool TryBuildAuthoritativeRansomSale(
        MobileParty playerParty,
        NetworkCompleteDoneLogic message,
        IReadOnlyCollection<Tuple<CharacterObject, CharacterObject, int>> upgrades,
        out TroopRoster requestedPrisoners,
        out string reason)
    {
        requestedPrisoners = null;
        reason = "The prisoner sale no longer matches the server state. Reopen the ransom screen and try again.";

        if (playerParty?.IsActive != true ||
            playerParty.MapEvent != null ||
            playerParty.CurrentSettlement?.Town == null ||
            HasAnyDelta(message.LeftMemberRosterData) ||
            HasAnyDelta(message.RightMemberRosterData) ||
            (upgrades?.Count ?? 0) != 0 ||
            (message.ReleasedPrisonersRoster?.Length ?? 0) != 0 ||
            (message.TakenPrisonersRoster?.Length ?? 0) != 0 ||
            (message.RecruitedPrisonersRoster?.Length ?? 0) != 0 ||
            message.ApplyReleasedAndTakenPrisonerActions ||
            !message.DoNotApplyGoldTransactions ||
            message.PartyInfluenceChangeAmount != 0 ||
            message.PartyMoraleChangeAmount != 0)
            return false;

        TroopRosterElementData[] left = message.LeftPrisonerRosterData.Data ??
            Array.Empty<TroopRosterElementData>();
        TroopRosterElementData[] right = message.RightPrisonerRosterData.Data ??
            Array.Empty<TroopRosterElementData>();
        if (left.Length == 0 || left.Length != right.Length ||
            left.Any(element => string.IsNullOrEmpty(element.CharacterId)) ||
            right.Any(element => string.IsNullOrEmpty(element.CharacterId)) ||
            left.Select(element => element.CharacterId)
                .Distinct(StringComparer.Ordinal).Count() != left.Length ||
            right.Select(element => element.CharacterId)
                .Distinct(StringComparer.Ordinal).Count() != right.Length)
            return false;

        Dictionary<string, TroopRosterElementData> rightById = right
            .ToDictionary(element => element.CharacterId, StringComparer.Ordinal);
        requestedPrisoners = new TroopRoster();
        foreach (TroopRosterElementData sale in left)
        {
            if (sale.Number <= 0 || sale.WoundedNumber < 0 ||
                sale.WoundedNumber > sale.Number || sale.Xp < 0 ||
                !rightById.TryGetValue(
                    sale.CharacterId, out TroopRosterElementData removal) ||
                removal.Number != -sale.Number ||
                removal.WoundedNumber != -sale.WoundedNumber ||
                removal.Xp != -sale.Xp ||
                !objectManager.TryGetObject(
                    sale.CharacterId, out CharacterObject character) ||
                character == null)
                return false;

            int availableIndex = playerParty.PrisonRoster
                .FindIndexOfTroop(character);
            if (availableIndex < 0)
                return false;
            TroopRosterElement available = playerParty.PrisonRoster
                .GetElementCopyAtIndex(availableIndex);
            int requestedHealthy = sale.Number - sale.WoundedNumber;
            int availableHealthy = available.Number - available.WoundedNumber;
            if (sale.Number > available.Number ||
                sale.WoundedNumber > available.WoundedNumber ||
                requestedHealthy > availableHealthy ||
                sale.Xp > available.Xp)
                return false;

            requestedPrisoners.AddToCounts(
                character,
                sale.Number,
                false,
                sale.WoundedNumber,
                sale.Xp,
                true);
        }

        return true;
    }

    private static bool IsAuthorizedPartySource(
        Hero mainHero,
        MobileParty playerParty,
        PartyBase leftParty,
        TroopRoster leftPrisonerRoster,
        TroopRoster questRoster,
        BattlePartyClaim battlePartyClaim,
        NetworkCompleteDoneLogic message,
        out bool allowBattleRescue)
    {
        allowBattleRescue = false;
        PartyBase source = leftParty ?? leftPrisonerRoster?.OwnerParty;
        if (source == null)
        {
            if (battlePartyClaim != null)
            {
                allowBattleRescue = true;
                // The prepared grant has already authenticated both ownerless
                // source deltas. Global conservation and role validation below
                // bind every positive destination delta to those sources while
                // still allowing the native screen to leave owned regulars behind.
                return true;
            }
            if (message.PartyScreenMode ==
                Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage)
                return questRoster != null;
            // Native dismiss/ransom/quest screens use ownerless dummy rosters.
            // Non-quest dummy rosters may receive units, but must never be used
            // as a source.
            return HasNoNegativeNumberDelta(message.LeftMemberRosterData) &&
                HasNoNegativeNumberDelta(message.LeftPrisonerRosterData);
        }
        if (source == playerParty?.Party)
            return true;
        if (source.Settlement != null &&
            source.Settlement == playerParty?.CurrentSettlement)
        {
            // A player may donate into a settlement they do not own, but may
            // only withdraw from a settlement controlled by their clan.
            return source.Settlement.OwnerClan == mainHero?.Clan ||
                HasNoNegativeNumberDelta(message.LeftMemberRosterData) &&
                HasNoNegativeNumberDelta(message.LeftPrisonerRosterData);
        }

        MobileParty mobile = source.MobileParty;
        if (mobile == null || playerParty == null)
            return false;
        if (mobile.ActualClan == mainHero?.Clan)
        {
            if (mobile.CurrentSettlement != null &&
                mobile.CurrentSettlement == playerParty.CurrentSettlement)
                return true;
            if (mobile.Army != null && mobile.Army == playerParty.Army)
                return true;
            if (mobile.CurrentSettlement != null ||
                playerParty.CurrentSettlement != null)
                return false;

            float radius = Campaign.Current.Models.EncounterModel
                .GetEncounterJoiningRadius;
            return playerParty.Position.ToVec2().Distance(
                mobile.Position.ToVec2()) <= radius;
        }

        // A defeated non-clan party may only provide losses during the active
        // map-event loot flow. In particular, a living enemy member cannot be
        // moved directly into the player's member roster. Rescued prisoners
        // may become members and are validated again by role conservation.
        MapEvent mapEvent = mobile.MapEvent;
        bool resolvedVictory = mapEvent != null &&
            (mapEvent.BattleState == BattleState.AttackerVictory ||
             mapEvent.BattleState == BattleState.DefenderVictory);
        if (!resolvedVictory || mapEvent != playerParty.MapEvent ||
            mobile.Party.MapEventSide == null ||
            playerParty.Party.MapEventSide == null ||
            mobile.Party.MapEventSide !=
                mapEvent.GetMapEventSide(mapEvent.DefeatedSide) ||
            playerParty.Party.MapEventSide !=
                mapEvent.GetMapEventSide(mapEvent.WinningSide) ||
            !message.ApplyReleasedAndTakenPrisonerActions ||
            !HasNoPositiveNumberDelta(message.LeftMemberRosterData) ||
            !HasNoPositiveNumberDelta(message.LeftPrisonerRosterData) ||
            !HasNoNegativeNumberDelta(message.RightMemberRosterData) ||
            !HasNoNegativeNumberDelta(message.RightPrisonerRosterData) ||
            !RightMemberAdditionsComeFromLeftPrisoners(message))
            return false;

        allowBattleRescue = true;
        return true;
    }

    private static bool HasNoNegativeNumberDelta(TroopRosterData data)
    {
        return (data.Data ?? Array.Empty<TroopRosterElementData>())
            .All(element => element.Number >= 0);
    }

    private static bool HasNoPositiveNumberDelta(TroopRosterData data)
    {
        return (data.Data ?? Array.Empty<TroopRosterElementData>())
            .All(element => element.Number <= 0);
    }

    private static bool RightMemberAdditionsComeFromLeftPrisoners(
        NetworkCompleteDoneLogic message)
    {
        Dictionary<string, long> prisonerRemovals =
            SumNumberDeltasById(message.LeftPrisonerRosterData);
        Dictionary<string, long> memberAdditions =
            SumNumberDeltasById(message.RightMemberRosterData);
        foreach (var addition in memberAdditions.Where(pair => pair.Value > 0))
        {
            prisonerRemovals.TryGetValue(addition.Key, out long removal);
            if (removal > -addition.Value)
                return false;
        }
        return true;
    }

    private static Dictionary<string, long> SumNumberDeltasById(
        TroopRosterData data)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (TroopRosterElementData element in
                 data.Data ?? Array.Empty<TroopRosterElementData>())
        {
            if (string.IsNullOrEmpty(element.CharacterId))
                continue;
            result.TryGetValue(element.CharacterId, out long current);
            result[element.CharacterId] = current + element.Number;
        }
        return result;
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

    private bool TryResolveCompleteDoneLogic(
        NetworkCompleteDoneLogic message,
        out PartyBase leftParty,
        out TroopRoster leftPrisonerRoster,
        out List<Tuple<CharacterObject, CharacterObject, int>> upgradedTroopHistory)
    {
        leftParty = null;
        leftPrisonerRoster = null;
        upgradedTroopHistory = null;

        if (message.LeftPartyId != null && !objectManager.TryGetObjectWithLogging<PartyBase>(message.LeftPartyId, out leftParty)) return false;
        if (message.LeftPrisonerRosterId != null && !objectManager.TryGetObjectWithLogging<TroopRoster>(message.LeftPrisonerRosterId, out leftPrisonerRoster)) return false;

        upgradedTroopHistory = ResolveUpgradedTroopHistory(message.UpgradedTroopHistoryIds);
        return true;
    }

    private List<Tuple<CharacterObject, CharacterObject, int>> ResolveUpgradedTroopHistory(UpgradedTroopHistoryData upgradedTroopHistoryIds)
    {
        List<Tuple<CharacterObject, CharacterObject, int>> upgradedTroopHistory = new();
        if (upgradedTroopHistoryIds.Data == null) return upgradedTroopHistory;

        foreach (var elementData in upgradedTroopHistoryIds.Data)
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(elementData.Character1Id, out var character1)) continue;
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(elementData.Character2Id, out var character2)) continue;

            upgradedTroopHistory.Add(new(character1, character2, elementData.Number));
        }

        return upgradedTroopHistory;
    }

    private static List<(TroopRoster roster, TroopRosterData delta)> CreateRosterDeltas(
        Hero mainHero,
        PartyBase leftParty,
        TroopRoster leftPrisonerRoster,
        TroopRoster questRoster,
        NetworkCompleteDoneLogic message,
        TroopRosterData leftPrisonerRosterData,
        TroopRosterData rightPrisonerRosterData)
    {
        // Collect every roster delta and apply them together: TryApplyTroopRosterDeltas removes before it
        // adds across all rosters, so a hero/prisoner moved between parties keeps its party linkage
        // (the destination addition is the last AddToCounts on that hero).
        var rosterDeltas = new List<(TroopRoster roster, TroopRosterData delta)>();
        if (leftParty != null)
        {
            rosterDeltas.Add((leftParty.MemberRoster, message.LeftMemberRosterData));
            rosterDeltas.Add((leftParty.PrisonRoster, leftPrisonerRosterData));
        }
        else if (leftPrisonerRoster != null) // Prisoner management doesn't have a set party
        {
            rosterDeltas.Add((leftPrisonerRoster, leftPrisonerRosterData));
        }
        else if (questRoster != null)
        {
            rosterDeltas.Add((questRoster, message.LeftMemberRosterData));
        }

        rosterDeltas.Add((mainHero.PartyBelongedTo.MemberRoster, message.RightMemberRosterData));
        rosterDeltas.Add((mainHero.PartyBelongedTo.PrisonRoster, rightPrisonerRosterData));
        return rosterDeltas;
    }

    private bool TryValidatePartyCommit(
        Hero mainHero,
        NetworkCompleteDoneLogic message,
        IReadOnlyCollection<Tuple<CharacterObject, CharacterObject, int>> upgrades,
        IReadOnlyCollection<(TroopRoster roster, TroopRosterData delta)> deltas,
        bool hasPrisonerDonation,
        bool hasGarrisonDonation,
        int? questGoldChange,
        IEnumerable<TroopRosterData> virtualSourceDeltas,
        out string reason)
    {
        reason = "The party changes were not valid for the current server state.";
        if (message.DoNotApplyGoldTransactions !=
            (message.PartyScreenMode ==
             Helpers.PartyScreenHelper.PartyScreenMode.Ransom))
            return false;
        var globalDeltas = new Dictionary<CharacterObject,
            (long number, long wounded, long xp)>();
        IEnumerable<TroopRosterData> allDeltas =
            deltas.Select(entry => entry.delta).Concat(
                virtualSourceDeltas ?? Enumerable.Empty<TroopRosterData>());
        foreach (TroopRosterData delta in allDeltas)
        {
            foreach (TroopRosterElementData element in
                     delta.Data ?? Array.Empty<TroopRosterElementData>())
            {
                if (!objectManager.TryGetObject(
                        element.CharacterId,
                        out CharacterObject character) || character == null)
                    return false;
                globalDeltas.TryGetValue(character, out var current);
                globalDeltas[character] = (
                    current.number + element.Number,
                    current.wounded + element.WoundedNumber,
                    current.xp + element.Xp);
            }
        }

        long goldCost = 0;
        var requiredItems = new Dictionary<ItemCategory, int>();
        var requiredSourceCounts = new Dictionary<CharacterObject, int>();
        var requiredTargetCounts = new Dictionary<CharacterObject, int>();
        var requiredSourceXp = new Dictionary<CharacterObject, long>();
        var expectedNumberDeltas = new Dictionary<CharacterObject, long>();
        var expectedXpDeltas = new Dictionary<CharacterObject, long>();
        foreach (Tuple<CharacterObject, CharacterObject, int> upgrade in
                 upgrades ?? Array.Empty<Tuple<CharacterObject, CharacterObject, int>>())
        {
            CharacterObject source = upgrade.Item1;
            CharacterObject target = upgrade.Item2;
            int count = upgrade.Item3;
            if (source == null || target == null || count <= 0)
                return false;
            int targetIndex = Array.IndexOf(source.UpgradeTargets, target);
            if (targetIndex < 0)
                return false;

            goldCost += (long)source.GetUpgradeGoldCost(
                mainHero.PartyBelongedTo.Party,
                targetIndex) * count;
            long xpCost = (long)source.GetUpgradeXpCost(
                mainHero.PartyBelongedTo.Party,
                targetIndex) * count;
            AddCount(requiredSourceCounts, source, count);
            AddCount(requiredTargetCounts, target, count);
            AddLongCount(expectedNumberDeltas, source, -count);
            AddLongCount(expectedNumberDeltas, target, count);
            AddLongCount(expectedXpDeltas, source, -xpCost);
            requiredSourceXp.TryGetValue(source, out long existingXp);
            requiredSourceXp[source] = existingXp + xpCost;

            ItemCategory category = target.UpgradeRequiresItemFromCategory;
            if (category != null)
            {
                requiredItems.TryGetValue(category, out int required);
                if (required > int.MaxValue - count)
                    return false;
                requiredItems[category] = required + count;
            }
        }

        foreach (var required in requiredSourceCounts)
        {
            if (!globalDeltas.TryGetValue(required.Key, out var delta) ||
                delta.number > -required.Value ||
                delta.xp > -requiredSourceXp[required.Key])
                return false;
        }
        foreach (var required in requiredTargetCounts)
        {
            if (!globalDeltas.TryGetValue(required.Key, out var delta) ||
                delta.number < required.Value)
                return false;
        }

        // Transfers between rosters net to zero. Upgrades are the only valid
        // source of positive global troop/XP/wounded deltas. Extra negative
        // number deltas are dismissals, but a wounded-only negative delta is
        // healing and must never be accepted.
        long totalWoundedDelta = 0;
        foreach (CharacterObject character in globalDeltas.Keys
                     .Union(expectedNumberDeltas.Keys)
                     .Union(expectedXpDeltas.Keys))
        {
            globalDeltas.TryGetValue(character, out var actual);
            expectedNumberDeltas.TryGetValue(
                character, out long expectedNumber);
            expectedXpDeltas.TryGetValue(character, out long expectedXp);
            if (actual.number > expectedNumber ||
                actual.xp > expectedXp ||
                actual.wounded < 0 &&
                    (actual.number >= 0 || actual.wounded < actual.number) ||
                actual.wounded > 0 &&
                    (!requiredTargetCounts.TryGetValue(
                        character, out int upgradedTargetCount) ||
                     actual.wounded > upgradedTargetCount))
                return false;
            totalWoundedDelta += actual.wounded;
        }
        if (totalWoundedDelta > 0)
            return false;

        bool isRansom = message.PartyScreenMode ==
            Helpers.PartyScreenHelper.PartyScreenMode.Ransom;
        if (!isRansom && !TryValidateUpgradeItems(
                mainHero.PartyBelongedTo.ItemRoster,
                message.RightOwnerPartyItemRosterData,
                requiredItems))
            return false;

        if (message.PartyScreenMode ==
            Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage)
        {
            if (!questGoldChange.HasValue ||
                message.PartyGoldChangeAmount != questGoldChange.Value ||
                questGoldChange.Value < 0 &&
                    mainHero.Gold < -(long)questGoldChange.Value)
                return false;
        }
        else if (!message.DoNotApplyGoldTransactions)
        {
            if (goldCost > int.MaxValue ||
                message.PartyGoldChangeAmount != -(int)goldCost ||
                mainHero.Gold < goldCost)
                return false;
        }

        if (!hasPrisonerDonation && !hasGarrisonDonation &&
            message.PartyInfluenceChangeAmount != 0)
            return false;

        return true;
    }

    private bool TryValidateRosterRoleChanges(
        MobileParty playerParty,
        FlattenedTroopRoster recruited,
        FlattenedTroopRoster taken,
        IReadOnlyCollection<Tuple<CharacterObject, CharacterObject, int>> upgrades,
        TroopRosterData leftMemberDelta,
        TroopRosterData leftPrisonerDelta,
        TroopRosterData rightMemberDelta,
        TroopRosterData rightPrisonerDelta,
        bool allowBattleRescue,
        BattlePartyClaim battlePartyClaim)
    {
        Dictionary<CharacterObject, int> recruitedCounts =
            GetFlattenedCounts(recruited);
        Dictionary<CharacterObject, int> takenCounts =
            GetFlattenedCounts(taken);
        Dictionary<CharacterObject, long> rightMemberCounts =
            SumNumberDeltas(rightMemberDelta);
        Dictionary<CharacterObject, long> rightPrisonerCounts =
            SumNumberDeltas(rightPrisonerDelta);
        Dictionary<CharacterObject, long> leftMemberCounts =
            SumNumberDeltas(leftMemberDelta);
        Dictionary<CharacterObject, long> leftPrisonerCounts =
            SumNumberDeltas(leftPrisonerDelta);
        var expectedUpgradeMember = new Dictionary<CharacterObject, long>();
        foreach (Tuple<CharacterObject, CharacterObject, int> upgrade in
                 upgrades ?? Array.Empty<Tuple<CharacterObject, CharacterObject, int>>())
        {
            AddLongCount(expectedUpgradeMember, upgrade.Item1, -upgrade.Item3);
            AddLongCount(expectedUpgradeMember, upgrade.Item2, upgrade.Item3);
        }

        var characters = new HashSet<CharacterObject>(rightMemberCounts.Keys);
        characters.UnionWith(rightPrisonerCounts.Keys);
        characters.UnionWith(leftMemberCounts.Keys);
        characters.UnionWith(leftPrisonerCounts.Keys);
        characters.UnionWith(recruitedCounts.Keys);
        characters.UnionWith(takenCounts.Keys);
        characters.UnionWith(expectedUpgradeMember.Keys);
        foreach (CharacterObject character in characters)
        {
            rightMemberCounts.TryGetValue(character, out long rightMember);
            rightPrisonerCounts.TryGetValue(character, out long rightPrisoner);
            leftMemberCounts.TryGetValue(character, out long leftMember);
            leftPrisonerCounts.TryGetValue(character, out long leftPrisoner);
            recruitedCounts.TryGetValue(character, out int recruitedCount);
            takenCounts.TryGetValue(character, out int takenCount);
            expectedUpgradeMember.TryGetValue(
                character, out long upgradeMember);

            if (battlePartyClaim != null)
            {
                long claimedMembers = Math.Max(0L, -leftMember);
                long claimedPrisoners = Math.Max(0L, -leftPrisoner);
                long grantMaximumMember =
                    upgradeMember + recruitedCount + claimedMembers;
                long grantMaximumPrisoner =
                    claimedPrisoners - recruitedCount;
                if (rightMember > grantMaximumMember ||
                    rightPrisoner > grantMaximumPrisoner)
                    return false;
                if (recruitedCount > 0 &&
                    (rightMember < recruitedCount ||
                     rightPrisoner - claimedPrisoners > -recruitedCount ||
                     playerParty == null ||
                     Campaign.Current.Models
                         .PrisonerRecruitmentCalculationModel
                         .CalculateRecruitableNumber(
                             playerParty.Party, character) < recruitedCount))
                    return false;
                continue;
            }

            long rescuedCount = allowBattleRescue
                ? Math.Min(Math.Max(0L, rightMember),
                    Math.Max(0L, -leftPrisoner))
                : 0L;
            long transferredPrisoners = Math.Min(
                Math.Max(0L, rightPrisoner),
                Math.Max(0L, -leftPrisoner));
            long capturedCount = allowBattleRescue
                ? Math.Max(0L, rightPrisoner - transferredPrisoners)
                : 0L;
            if (allowBattleRescue &&
                (capturedCount > Math.Max(0L, -leftMember) ||
                 takenCount != capturedCount))
                return false;
            long actualMember = leftMember + rightMember;
            long actualPrisoner = leftPrisoner + rightPrisoner;
            long maximumMember = upgradeMember + recruitedCount + rescuedCount;
            long maximumPrisoner =
                capturedCount -
                recruitedCount - rescuedCount;

            // Dismissals/releases may make a role more negative, but only
            // upgrades, qualified recruitment, battle capture and rescued
            // prisoners may create a positive role delta.
            if (actualMember > maximumMember ||
                actualPrisoner > maximumPrisoner)
                return false;

            if (recruitedCount > 0 &&
                (rightMember < recruitedCount ||
                 rightPrisoner > -recruitedCount ||
                 playerParty == null ||
                Campaign.Current.Models.PrisonerRecruitmentCalculationModel
                    .CalculateRecruitableNumber(
                        playerParty.Party, character) < recruitedCount))
                return false;
        }
        return true;
    }

    private Dictionary<CharacterObject, long> SumNumberDeltas(
        TroopRosterData data)
    {
        var result = new Dictionary<CharacterObject, long>();
        foreach (TroopRosterElementData element in
                 data.Data ?? Array.Empty<TroopRosterElementData>())
        {
            if (!objectManager.TryGetObject(
                    element.CharacterId,
                    out CharacterObject character) || character == null)
                return new Dictionary<CharacterObject, long>();
            result.TryGetValue(character, out long current);
            result[character] = current + element.Number;
        }
        return result;
    }

    private static void AddCount(
        IDictionary<CharacterObject, int> counts,
        CharacterObject character,
        int amount)
    {
        counts.TryGetValue(character, out int current);
        counts[character] = current + amount;
    }

    private static void AddLongCount(
        IDictionary<CharacterObject, long> counts,
        CharacterObject character,
        long amount)
    {
        counts.TryGetValue(character, out long current);
        counts[character] = current + amount;
    }

    private static bool TryValidateUpgradeItems(
        ItemRoster currentRoster,
        ItemRosterElement[] submittedRoster,
        IReadOnlyDictionary<ItemCategory, int> requiredItems)
    {
        if (currentRoster == null || submittedRoster == null)
            return false;
        var current = new Dictionary<EquipmentElement, int>();
        foreach (ItemRosterElement element in currentRoster)
        {
            if (element.EquipmentElement.Item == null || element.Amount < 0)
                return false;
            current[element.EquipmentElement] = element.Amount;
        }
        var submitted = new Dictionary<EquipmentElement, int>();
        foreach (ItemRosterElement element in submittedRoster)
        {
            if (element.EquipmentElement.Item == null)
            {
                // Older clients sent ItemRoster._data rather than ToArray().
                // Its unused capacity is represented by null/zero rows and
                // carries no state. A null row with a non-zero amount remains
                // invalid and is rejected.
                if (element.Amount == 0)
                    continue;
                return false;
            }
            if (element.Amount < 0 ||
                submitted.ContainsKey(element.EquipmentElement))
                return false;
            if (element.Amount > 0)
                submitted[element.EquipmentElement] = element.Amount;
        }

        var removedByCategory = new Dictionary<ItemCategory, int>();
        foreach (EquipmentElement element in current.Keys.Union(submitted.Keys))
        {
            current.TryGetValue(element, out int before);
            submitted.TryGetValue(element, out int after);
            if (after > before)
                return false;
            int removed = before - after;
            if (removed == 0)
                continue;
            ItemCategory category = element.Item?.ItemCategory;
            if (category == null || !requiredItems.ContainsKey(category))
                return false;
            removedByCategory.TryGetValue(category, out int existing);
            removedByCategory[category] = existing + removed;
        }

        return requiredItems.Count == removedByCategory.Count &&
            requiredItems.All(pair =>
                removedByCategory.TryGetValue(pair.Key, out int removed) &&
                removed == pair.Value);
    }

    private void PublishPlayerCaptivityReleaseEvents(List<PlayerCaptivityEndedByServer> releasedPlayerCaptivityEvents)
    {
        foreach (var releaseEvent in releasedPlayerCaptivityEvents)
        {
            messageBroker.Publish(this, releaseEvent);
        }
    }

    private static void ApplyRightOwnerPartyItemRoster(Hero mainHero, NetworkCompleteDoneLogic message)
    {
        mainHero.PartyBelongedTo.ItemRoster.Clear();
        foreach (var itemRosterElement in message.RightOwnerPartyItemRosterData ?? Enumerable.Empty<ItemRosterElement>())
        {
            // v1.4.17 and older clients serialized ItemRoster._data, whose unused
            // capacity is represented by default null/zero rows. Validation above
            // rejects any state-bearing null row; do not feed harmless padding back
            // into Bannerlord's roster implementation during a compatible retry.
            if (itemRosterElement.EquipmentElement.Item == null &&
                itemRosterElement.Amount == 0)
                continue;
            mainHero.PartyBelongedTo.ItemRoster.Add(itemRosterElement);
        }
    }

    private HashSet<string> GetTakenHeroCharacterIds(FlattenedTroopRoster takenPrisonersRoster)
    {
        var characterIds = new HashSet<string>();
        foreach (var element in takenPrisonersRoster)
        {
            if (element.Troop?.IsHero == true &&
                objectManager.TryGetIdWithLogging(element.Troop, out var characterId))
                characterIds.Add(characterId);
        }

        return characterIds;
    }

    private bool TryValidatePrisonerActionRosters(
        FlattenedTroopRoster releasedPrisonersRoster,
        FlattenedTroopRoster takenPrisonersRoster,
        TroopRosterData rightPrisonerRosterData,
        out HashSet<string> takenHeroCharacterIds)
    {
        takenHeroCharacterIds = GetTakenHeroCharacterIds(takenPrisonersRoster);
        var signedDeltas = (rightPrisonerRosterData.Data ?? Array.Empty<TroopRosterElementData>())
            .GroupBy(element => element.CharacterId)
            .ToDictionary(group => group.Key, group => group.Sum(element => element.Number));
        Dictionary<string, int> released =
            GetActionCountsById(releasedPrisonersRoster);
        Dictionary<string, int> taken =
            GetActionCountsById(takenPrisonersRoster);
        if (released == null || taken == null)
            return false;

        var characterIds = new HashSet<string>(signedDeltas.Keys);
        characterIds.UnionWith(released.Keys);
        characterIds.UnionWith(taken.Keys);
        return characterIds.All(characterId =>
        {
            signedDeltas.TryGetValue(characterId, out int delta);
            released.TryGetValue(characterId, out int releasedCount);
            taken.TryGetValue(characterId, out int takenCount);
            return delta == takenCount - releasedCount;
        });
    }

    private static void TryPostPartyEffect(
        Action action,
        string mainHeroId,
        string operation)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception exception)
        {
            logger.Error(
                exception,
                "Party commit completed for {MainHeroId}, but {Operation} failed",
                mainHeroId,
                operation);
        }
    }

    private bool TryValidatePrisonerDonation(
        DonatePrisoners donation,
        MobileParty playerParty,
        FlattenedTroopRoster releasedPrisoners,
        TroopRosterData rightPrisonerDelta,
        out string reason)
    {
        reason = "The prisoner donation no longer matches the server state.";
        Settlement settlement = playerParty?.CurrentSettlement;
        if (settlement == null ||
            !objectManager.TryGetId(settlement, out string settlementId) ||
            !objectManager.TryGetId(playerParty.Party, out string partyId) ||
            !string.Equals(
                donation.CurrentSettlementId,
                settlementId,
                StringComparison.Ordinal) ||
            !string.Equals(
                donation.RightPartyId,
                partyId,
                StringComparison.Ordinal))
            return false;

        FlattenedTroopRoster staged = FlattenedTroopSerializer.Deserialize(
            donation.RightSidePrisonerRoster,
            objectManager);
        Dictionary<CharacterObject, int> releasedCounts =
            GetFlattenedCounts(releasedPrisoners);
        Dictionary<CharacterObject, int> stagedCounts =
            GetFlattenedCounts(staged);
        if (releasedCounts.Count == 0 ||
            releasedCounts.Count != stagedCounts.Count ||
            releasedCounts.Any(pair =>
                !stagedCounts.TryGetValue(pair.Key, out int count) ||
                count != pair.Value))
            return false;

        var signedDeltas = new Dictionary<CharacterObject, int>();
        foreach (TroopRosterElementData element in
                 rightPrisonerDelta.Data ?? Array.Empty<TroopRosterElementData>())
        {
            if (!objectManager.TryGetObject(
                    element.CharacterId,
                    out CharacterObject character) ||
                character == null || signedDeltas.ContainsKey(character))
                return false;
            if (element.Number != 0)
                signedDeltas.Add(character, element.Number);
        }

        return signedDeltas.Count == releasedCounts.Count &&
            releasedCounts.All(pair =>
                signedDeltas.TryGetValue(pair.Key, out int delta) &&
                delta == -pair.Value);
    }

    private static Dictionary<CharacterObject, int> GetFlattenedCounts(
        FlattenedTroopRoster roster)
    {
        var counts = new Dictionary<CharacterObject, int>();
        if (roster == null)
            return counts;
        foreach (FlattenedTroopRosterElement element in roster)
        {
            if (element.Troop == null)
                continue;
            counts.TryGetValue(element.Troop, out int count);
            counts[element.Troop] = count + 1;
        }
        return counts;
    }

    private bool TryValidateGarrisonDonation(
        DonateToGarrison donation,
        MobileParty playerParty,
        NetworkCompleteDoneLogic message,
        out Settlement settlement,
        out MobileParty garrison,
        out bool createdGarrison,
        out string reason)
    {
        settlement = playerParty?.CurrentSettlement;
        garrison = settlement?.Town?.GarrisonParty;
        createdGarrison = false;
        reason = "The garrison donation no longer matches the server state.";
        if (settlement?.Town == null ||
            message.PartyScreenMode !=
                Helpers.PartyScreenHelper.PartyScreenMode.TroopsManage ||
            message.LeftPartyId != null ||
            !objectManager.TryGetId(settlement, out string settlementId) ||
            !string.Equals(
                donation.CurrentSettlementId,
                settlementId,
                StringComparison.Ordinal))
            return false;

        TroopRosterElementData[] leftData =
            message.LeftMemberRosterData.Data ??
            Array.Empty<TroopRosterElementData>();
        TroopRosterElementData[] rightData =
            message.RightMemberRosterData.Data ??
            Array.Empty<TroopRosterElementData>();
        if (leftData.Any(element => string.IsNullOrEmpty(element.CharacterId)) ||
            rightData.Any(element => string.IsNullOrEmpty(element.CharacterId)) ||
            leftData.Select(element => element.CharacterId).Distinct().Count() !=
                leftData.Length ||
            rightData.Select(element => element.CharacterId).Distinct().Count() !=
                rightData.Length)
            return false;
        var left = leftData.ToDictionary(element => element.CharacterId);
        var right = rightData.ToDictionary(element => element.CharacterId);
        if (left.Count == 0 || left.Count != right.Count)
            return false;
        if (donation.Troops == null || donation.Troops.Count == 0 ||
            donation.Troops.Any(troop =>
                string.IsNullOrEmpty(troop.CharacterId) || troop.Count <= 0))
            return false;
        Dictionary<string, int> staged = donation.Troops
            .GroupBy(troop => troop.CharacterId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(troop => troop.Count),
                StringComparer.Ordinal);
        if (staged.Count != left.Count)
            return false;
        long donated = 0;
        foreach (var pair in left)
        {
            TroopRosterElementData add = pair.Value;
            if (add.Number <= 0 || add.WoundedNumber < 0 ||
                add.WoundedNumber > add.Number || add.Xp < 0 ||
                !staged.TryGetValue(pair.Key, out int stagedCount) ||
                stagedCount != add.Number ||
                !right.TryGetValue(pair.Key, out TroopRosterElementData remove) ||
                remove.Number != -add.Number ||
                remove.WoundedNumber != -add.WoundedNumber ||
                remove.Xp != -add.Xp)
                return false;
            donated += add.Number;
        }
        if (donated > int.MaxValue)
            return false;

        if (garrison == null)
        {
            try
            {
                settlement.AddGarrisonParty();
                garrison = settlement.Town.GarrisonParty;
                createdGarrison = garrison != null;
            }
            catch (Exception exception)
            {
                logger.Error(
                    exception,
                    "Failed to create a garrison for donation at {Settlement}",
                    settlement.StringId);
                return false;
            }
        }
        if (garrison == null)
            return false;

        bool fits = (long)garrison.Party.NumberOfAllMembers + donated <=
            garrison.Party.PartySizeLimit;
        if (!fits)
            RollbackCreatedDonationGarrison(garrison, createdGarrison);
        return fits;
    }

    private static void RollbackCreatedDonationGarrison(
        MobileParty garrison,
        bool createdGarrison)
    {
        if (!createdGarrison || garrison == null ||
            garrison.MemberRoster.TotalManCount != 0 ||
            garrison.PrisonRoster.TotalManCount != 0)
            return;
        try
        {
            DestroyPartyAction.Apply(null, garrison);
        }
        catch (Exception exception)
        {
            logger.Error(
                exception,
                "Failed to roll back a newly-created empty garrison {Party}",
                garrison.StringId);
        }
    }

    private Dictionary<string, int> GetActionCountsById(
        FlattenedTroopRoster actionRoster)
    {
        var actionCounts = new Dictionary<string, int>();
        foreach (var element in actionRoster)
        {
            if (element.Troop == null ||
                !objectManager.TryGetIdWithLogging(element.Troop, out var characterId))
                return null;

            actionCounts.TryGetValue(characterId, out var count);
            actionCounts[characterId] = count + 1;
        }

        return actionCounts;
    }

    internal static TroopRosterData FilterTakenHeroAdditions(
        TroopRosterData delta,
        HashSet<string> takenHeroCharacterIds)
    {
        if (delta.Data == null || takenHeroCharacterIds.Count == 0)
            return delta;

        var filtered = delta.Data
            .Where(element => element.Number <= 0 || !takenHeroCharacterIds.Contains(element.CharacterId))
            .ToArray();
        return filtered.Length == delta.Data.Length
            ? delta
            : new TroopRosterData(filtered);
    }

    internal static bool HasLeftPrisonerTransferDestination(
        bool applyReleasedAndTakenPrisonerActions,
        bool hasLeftParty,
        bool hasLeftPrisonerRoster)
        => !applyReleasedAndTakenPrisonerActions && (hasLeftParty || hasLeftPrisonerRoster);

    private static void ApplyReleasedAndTakenPrisonerActions(
        Hero mainHero,
        FlattenedTroopRoster releasedPrisonersRoster,
        FlattenedTroopRoster takenPrisonersRoster)
    {
        var nonPlayerReleases = new FlattenedTroopRoster(4);
        foreach (var element in releasedPrisonersRoster)
        {
            if (element.Troop?.HeroObject?.IsPlayerHero() != true)
                nonPlayerReleases[element.Descriptor] = element;
        }

        if (!nonPlayerReleases.IsEmpty<FlattenedTroopRosterElement>())
            EndCaptivityAction.ApplyByReleasedByChoice(nonPlayerReleases);

        if (takenPrisonersRoster.IsEmpty<FlattenedTroopRosterElement>())
            return;

        var captorParty = mainHero.PartyBelongedTo?.Party;
        if (captorParty == null)
        {
            logger.Error("Cannot apply Party screen prisoner captures because main hero {Hero} has no party", mainHero);
            return;
        }

        foreach (var element in takenPrisonersRoster)
        {
            if (element.Troop?.HeroObject is Hero hero)
                TakePrisonerAction.Apply(captorParty, hero);
        }
        CampaignEventDispatcher.Instance.OnPrisonerTaken(takenPrisonersRoster);
    }

    private static void NotifyTakenPrisonersChanged(FlattenedTroopRoster takenPrisonersRoster)
    {
        if (Settlement.CurrentSettlement == null) return;
        if (takenPrisonersRoster.IsEmpty<FlattenedTroopRosterElement>()) return;

        CampaignEventDispatcher.Instance.OnPrisonersChangeInSettlement(Settlement.CurrentSettlement, takenPrisonersRoster, null, true);
    }

    private static void ApplyPartyRewardChanges(
        Hero mainHero,
        NetworkCompleteDoneLogic message,
        bool suppressInfluence)
    {
        if (!message.DoNotApplyGoldTransactions)
        {
            GiveGoldAction.ApplyBetweenCharacters(null, mainHero, message.PartyGoldChangeAmount, false);
        }
        if (!suppressInfluence && message.PartyInfluenceChangeAmount != 0)
        {
            // Influence goes to the requesting player's clan (mainHero), not the local machine's
            // Hero.MainHero - which is null on a dedicated server (NRE) and the wrong clan otherwise.
            GainKingdomInfluenceAction.ApplyForLeavingTroopToGarrison(mainHero, (float)message.PartyInfluenceChangeAmount);
        }
    }

    private static void ApplyPrisonerDonation(
        MobileParty playerParty,
        FlattenedTroopRoster donatedPrisoners,
        Settlement settlement)
    {
        if (playerParty == null || settlement == null ||
            donatedPrisoners == null ||
            donatedPrisoners.IsEmpty<FlattenedTroopRosterElement>())
            return;
        float influence = 0f;
        foreach (CharacterObject character in donatedPrisoners.Troops)
        {
            if (character?.IsHero == true)
                EnterSettlementAction.ApplyForPrisoner(
                    character.HeroObject,
                    settlement);
            if (character != null)
                influence += Campaign.Current.Models.PrisonerDonationModel
                    .CalculateInfluenceGainAfterPrisonerDonation(
                        playerParty.Party, character, settlement);
        }
        CampaignEventDispatcher.Instance.OnPrisonerDonatedToSettlement(
            playerParty,
            donatedPrisoners,
            settlement);
        if (influence > 0f && playerParty.LeaderHero != null)
            GainKingdomInfluenceAction.ApplyForLeavingTroopToGarrison(
                playerParty.LeaderHero, (int)influence);
    }

    private void ApplyGarrisonDonation(
        Hero mainHero,
        Settlement settlement,
        TroopRosterData donatedData)
    {
        if (mainHero?.PartyBelongedTo == null || settlement == null)
            return;
        TroopRoster donated = TroopRoster.CreateDummyTroopRoster();
        float influence = 0f;
        foreach (TroopRosterElementData data in
                 donatedData.Data ?? Array.Empty<TroopRosterElementData>())
        {
            if (!objectManager.TryGetObject(
                    data.CharacterId, out CharacterObject character) ||
                character == null || data.Number <= 0)
                continue;
            donated.AddToCounts(
                character,
                data.Number,
                false,
                data.WoundedNumber,
                data.Xp,
                true,
                -1);
            if (character.IsHero)
                EnterSettlementAction.ApplyForCharacterOnly(
                    character.HeroObject, settlement);
            influence += data.Number * Campaign.Current.Models
                .PrisonerDonationModel
                .CalculateInfluenceGainAfterTroopDonation(
                    mainHero.PartyBelongedTo.Party,
                    character,
                    settlement);
        }
        CampaignEventDispatcher.Instance.OnTroopGivenToSettlement(
            mainHero, settlement, donated);
        if (influence > 0f)
            GainKingdomInfluenceAction.ApplyForLeavingTroopToGarrison(
                mainHero, (int)influence);
    }

    private static void ApplyUpgradedTroopHistory(Hero mainHero, List<Tuple<CharacterObject, CharacterObject, int>> upgradedTroopHistory)
    {
        //Replacement for CampaignEventDispatcher.Instance.OnPlayerUpgradedTroops(tuple.Item1, tuple.Item2, tuple.Item3) without MainParty
        foreach (Tuple<CharacterObject, CharacterObject, int> tuple in upgradedTroopHistory)
        {
            SkillLevelingManager.OnUpgradeTroops(mainHero.PartyBelongedTo.Party, tuple.Item1, tuple.Item2, tuple.Item3);
        }
    }

    private static void ApplyPrisonerRecruitmentEffects(
        Hero mainHero,
        NetworkCompleteDoneLogic message,
        FlattenedTroopRoster recruitedPrisonersRoster)
    {
        if (message.RecruitedPrisonersRoster == null) return;
        if (recruitedPrisonersRoster.IsEmpty<FlattenedTroopRosterElement>()) return;

        // Replacement for CampaignEventDispatcher.Instance.OnMainPartyPrisonerRecruited(obj.What.RecruitedPrisonersRoster);
        foreach (CharacterObject characterObject in recruitedPrisonersRoster.Troops)
        {
            ApplyPrisonerRecruitmentEffect(mainHero, characterObject);
        }
    }

    private static void ApplyPrisonerRecruitmentEffect(Hero mainHero, CharacterObject characterObject)
    {
        // Replace CampaignEventDispatcher.Instance.OnUnitRecruited(characterObject, 1);
        if (mainHero.GetPerkValue(DefaultPerks.Leadership.FamousCommander))
        {
            mainHero.PartyBelongedTo.MemberRoster.AddXpToTroop(characterObject, (int)DefaultPerks.Leadership.FamousCommander.SecondaryBonus * 1);
        }
        SkillLevelingManager.OnTroopRecruited(mainHero, 1, characterObject.Tier);
        if (characterObject.Occupation == Occupation.Bandit)
        {
            SkillLevelingManager.OnBanditsRecruited(mainHero.PartyBelongedTo, characterObject, 1);
        }

        // Replace ApplyPrisonerRecruitmentEffects
        int prisonerRecruitmentMoraleEffect = Campaign.Current.Models.PrisonerRecruitmentCalculationModel.GetPrisonerRecruitmentMoraleEffect(mainHero.PartyBelongedTo.Party, characterObject, 1);
        mainHero.PartyBelongedTo.RecentEventsMorale += (float)prisonerRecruitmentMoraleEffect;
    }

    private void ApplyRosterOrder(TroopRoster roster, TroopRosterOrderData orderData)
    {
        messageBroker.Publish(this, new ApplyTroopRosterOrder(roster, orderData));
    }

    internal List<PlayerCaptivityEndedByServer> CreatePlayerCaptivityReleaseEvents(
        TroopRosterData leftPrisonerRosterData,
        TroopRosterData rightPrisonerRosterData,
        bool hasLeftPrisonerDestination,
        CampaignVec2 releaserPartyPosition,
        out TroopRosterData filteredLeftPrisonerRosterData,
        out TroopRosterData filteredRightPrisonerRosterData)
    {
        var releasedPlayerPrisoners = new List<Hero>();
        // The normal party screen's left prisoner roster is a dummy discard target, not a transfer destination.
        var transferredPlayerPrisoners = hasLeftPrisonerDestination
            ? GetTransferredPlayerPrisoners(leftPrisonerRosterData, rightPrisonerRosterData)
            : GetTransferredPlayerPrisoners(rightPrisonerRosterData);
        filteredLeftPrisonerRosterData = FilterPlayerPrisonerReleaseDelta(leftPrisonerRosterData, transferredPlayerPrisoners, releasedPlayerPrisoners);
        filteredRightPrisonerRosterData = FilterPlayerPrisonerReleaseDelta(rightPrisonerRosterData, transferredPlayerPrisoners, releasedPlayerPrisoners);

        return releasedPlayerPrisoners
            .Select(playerHero => new PlayerCaptivityEndedByServer(playerHero, EndCaptivityDetail.ReleasedByChoice, null, releaserPartyPosition))
            .ToList();
    }

    private HashSet<string> GetTransferredPlayerPrisoners(params TroopRosterData[] prisonerRosterDeltas)
    {
        var transferredPlayerPrisoners = new HashSet<string>();
        foreach (var delta in prisonerRosterDeltas)
        {
            foreach (var elementData in delta.Data ?? Array.Empty<TroopRosterElementData>())
            {
                if (elementData.Number > 0 && TryGetPlayerPrisonerHero(elementData, out _))
                    transferredPlayerPrisoners.Add(elementData.CharacterId);
            }
        }

        return transferredPlayerPrisoners;
    }

    private TroopRosterData FilterPlayerPrisonerReleaseDelta(
        TroopRosterData delta,
        HashSet<string> transferredPlayerPrisoners,
        List<Hero> releasedPlayerPrisoners)
    {
        if (delta.Data == null) return delta;

        var filtered = new List<TroopRosterElementData>();
        foreach (var elementData in delta.Data)
        {
            if (elementData.Number < 0 &&
                TryGetPlayerPrisonerHero(elementData, out var playerHero) &&
                !transferredPlayerPrisoners.Contains(elementData.CharacterId))
            {
                releasedPlayerPrisoners.Add(playerHero);
                continue;
            }

            filtered.Add(elementData);
        }

        return filtered.Count == delta.Data.Length
            ? delta
            : new TroopRosterData(filtered);
    }

    private bool TryGetPlayerPrisonerHero(TroopRosterElementData elementData, out Hero playerHero)
    {
        playerHero = null;
        return objectManager.TryGetObjectWithLogging<CharacterObject>(elementData.CharacterId, out var character) &&
               (playerHero = character.HeroObject)?.IsPlayerHero() == true;
    }
}
