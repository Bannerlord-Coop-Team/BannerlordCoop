using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Services.Companions.Interfaces;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.TroopRosters.Messages;
using LiteNetLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.Companions.Handlers;

internal class CompanionRolesHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<CompanionRolesHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ICompanionRolesCampaignBehaviorInterface companionRolesCampaignBehaviorInterface;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPlayerManager playerManager;
    private readonly ISendCoalescer sendCoalescer;
    private string pendingFireCompanionRequestId;
    private string pendingFireCompanionHeroId;

    public CompanionRolesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ICompanionRolesCampaignBehaviorInterface companionRolesCampaignBehaviorInterface,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.companionRolesCampaignBehaviorInterface = companionRolesCampaignBehaviorInterface;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<ClanNameSelectionDone>(Handle_ClanNameSelectionDone);
        messageBroker.Subscribe<DoClanNameSelection>(Handle_DoClanNameSelection);
        messageBroker.Subscribe<CompanionFired>(Handle_CompanionFired);
        messageBroker.Subscribe<FireCompanion>(Handle_FireCompanion);
        messageBroker.Subscribe<FireCompanionCompleted>(Handle_FireCompanionCompleted);
        messageBroker.Subscribe<CompanionRejoinAfterEmprisonment>(Handle_CompanionRejoinAfterEmprisonment);
        messageBroker.Subscribe<DoCompanionRejoinAfterEmprisonment>(Handle_DoCompanionRejoinAfterEmprisonment);
        messageBroker.Subscribe<CompanionJoinedPartyByRescue>(Handle_CompanionJoinedPartyByRescue);
        messageBroker.Subscribe<DoCompanionJoinedPartyByRescue>(Handle_DoCompanionJoinedPartyByRescue);
        messageBroker.Subscribe<PartyScreenClosedFromRescuing>(Handle_PartyScreenClosedFromRescuing);
        messageBroker.Subscribe<DoPartyScreenClosedFromRescuing>(Handle_DoPartyScreenClosedFromRescuing);
        messageBroker.Subscribe<CompanionRescueCompleted>(Handle_CompanionRescueCompleted);
        messageBroker.Subscribe<CompanionRescued>(Handle_CompanionRescued);
        messageBroker.Subscribe<RescueCompanion>(Handle_RescueCompanion);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClanNameSelectionDone>(Handle_ClanNameSelectionDone);
        messageBroker.Unsubscribe<DoClanNameSelection>(Handle_DoClanNameSelection);
        messageBroker.Unsubscribe<CompanionFired>(Handle_CompanionFired);
        messageBroker.Unsubscribe<FireCompanion>(Handle_FireCompanion);
        messageBroker.Unsubscribe<FireCompanionCompleted>(Handle_FireCompanionCompleted);
        messageBroker.Unsubscribe<CompanionRejoinAfterEmprisonment>(Handle_CompanionRejoinAfterEmprisonment);
        messageBroker.Unsubscribe<DoCompanionRejoinAfterEmprisonment>(Handle_DoCompanionRejoinAfterEmprisonment);
        messageBroker.Unsubscribe<CompanionJoinedPartyByRescue>(Handle_CompanionJoinedPartyByRescue);
        messageBroker.Unsubscribe<DoCompanionJoinedPartyByRescue>(Handle_DoCompanionJoinedPartyByRescue);
        messageBroker.Unsubscribe<PartyScreenClosedFromRescuing>(Handle_PartyScreenClosedFromRescuing);
        messageBroker.Unsubscribe<DoPartyScreenClosedFromRescuing>(Handle_DoPartyScreenClosedFromRescuing);
        messageBroker.Unsubscribe<CompanionRescueCompleted>(Handle_CompanionRescueCompleted);
        messageBroker.Unsubscribe<CompanionRescued>(Handle_CompanionRescued);
        messageBroker.Unsubscribe<RescueCompanion>(Handle_RescueCompanion);
    }

    private void Handle_ClanNameSelectionDone(MessagePayload<ClanNameSelectionDone> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero, out var oneToOneConversationHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.SelectedFief, out var selectedFiefId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;

        var message = new DoClanNameSelection(
            mainHeroId,
            oneToOneConversationHeroId,
            selectedFiefId,
            mainPartyId,
            obj.What.ClanName
        );

        network.SendAll(message);
    }

    private void Handle_DoClanNameSelection(MessagePayload<DoClanNameSelection> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OneToOneConversationHeroId, out var oneToOneConversationHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SelectedFiefId, out var selectedFief)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyId, out var mainParty)) return;

            var companionRolesCampaignBehavior = Campaign.Current.GetCampaignBehavior<CompanionRolesCampaignBehavior>();

            RemoveCompanionAction.ApplyByByTurningToLord(mainHero.Clan, oneToOneConversationHero);
            oneToOneConversationHero.SetNewOccupation(Occupation.Lord);
            TextObject textObject = GameTexts.FindText("str_generic_clan_name", null);
            textObject.SetTextVariable("CLAN_NAME", new TextObject(data.ClanName, null));
            // Set ResolvedMainHero for the duration of these calls so ChangeRelationActionPatches
            // ,CompanionRolesPatches and ClanPatches can resolve the correct mainhero via the Harmony prefix instead of
            // Hero.MainHero which is different on the server compared to the client. Wrapped in try/finally so the static fields
            // are always cleared even if something below throws
            ResolvedMainHeroContext.ResolvedMainHero = mainHero;
            try
            { 
                int randomBannerIdForNewClan = companionRolesCampaignBehavior.GetRandomBannerIdForNewClan();
                Clan clan = Clan.CreateCompanionToLordClan(oneToOneConversationHero, selectedFief, textObject, randomBannerIdForNewClan);
                if (oneToOneConversationHero.PartyBelongedTo == mainParty)
                {
                    mainParty.MemberRoster.AddToCounts(oneToOneConversationHero.CharacterObject, -1, false, 0, 0, true, -1);
                }
                MobileParty partyBelongedTo = oneToOneConversationHero.PartyBelongedTo;
                if (partyBelongedTo == null)
                {
                    MobileParty mobileParty = LordPartyComponent.CreateLordParty(oneToOneConversationHero.CharacterObject.StringId, oneToOneConversationHero, mainParty.Position, 3f, selectedFief, oneToOneConversationHero);
                    mobileParty.MemberRoster.AddToCounts(clan.Culture.BasicTroop, MBRandom.RandomInt(12, 15), false, 0, 0, true, -1);
                    mobileParty.MemberRoster.AddToCounts(clan.Culture.EliteBasicTroop, MBRandom.RandomInt(10, 15), false, 0, 0, true, -1);
                }
                else
                {
                    partyBelongedTo.ActualClan = clan;
                    partyBelongedTo.Party.SetVisualAsDirty();
                }
                companionRolesCampaignBehavior.AdjustCompanionsEquipment(oneToOneConversationHero);
                companionRolesCampaignBehavior.SpawnNewHeroesForNewCompanionClan(oneToOneConversationHero, clan, selectedFief);
                GiveGoldAction.ApplyBetweenCharacters(mainHero, oneToOneConversationHero, 20000, false);
                GainKingdomInfluenceAction.ApplyForDefault(mainHero, -500f);
                ChangeRelationAction.ApplyPlayerRelation(oneToOneConversationHero, 50, true, true);
            }
            finally
            {
                ResolvedMainHeroContext.ResolvedMainHero = null;
            }
        });
    }

    private void Handle_CompanionFired(MessagePayload<CompanionFired> obj)
    {
        if (pendingFireCompanionRequestId != null)
        {
            logger.Warning("Ignored a second companion dismissal while request {RequestId} is pending",
                pendingFireCompanionRequestId);
            return;
        }

        var requestId = Guid.NewGuid().ToString("N");
        pendingFireCompanionRequestId = requestId;
        pendingFireCompanionHeroId = null;

        try
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero,
                out var oneToOneConversationHeroId))
                throw new InvalidOperationException("The companion being dismissed could not be resolved.");

            pendingFireCompanionHeroId = oneToOneConversationHeroId;
            if (obj.What.OneToOneConversationHero.CompanionOf == null)
                throw new InvalidOperationException("The companion has no owning clan.");
            if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero.CompanionOf,
                out var expectedClanId))
                throw new InvalidOperationException("The companion's owning clan could not be resolved.");

            string expectedPartyId = null;
            if (obj.What.OneToOneConversationHero.PartyBelongedTo != null &&
                !objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero.PartyBelongedTo,
                    out expectedPartyId))
                throw new InvalidOperationException("The companion's party could not be resolved.");

            network.SendAll(new FireCompanion(requestId, oneToOneConversationHeroId,
                expectedClanId, expectedPartyId));
            logger.Information("Sent companion dismissal request {RequestId} for {HeroId}",
                requestId, oneToOneConversationHeroId);
        }
        catch (Exception exception)
        {
            CompletePendingFireCompanion(requestId, pendingFireCompanionHeroId, false, exception.Message);
        }
    }

    private void Handle_FireCompanion(MessagePayload<FireCompanion> obj)
    {
        var data = obj.What;
        var requester = obj.Who as NetPeer;

        if (requester == null)
        {
            logger.Error("Rejected {Message} without a requesting peer", nameof(FireCompanion));
            return;
        }

        GameThread.RunSafe(() =>
        {
            bool success = false;
            string error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(data.RequestId))
                    throw new InvalidOperationException("The dismissal request has no correlation id.");
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.OneToOneConversationHeroId,
                    out var oneToOneConversationHero))
                    throw new InvalidOperationException("The requested companion could not be resolved.");
                if (oneToOneConversationHero.CompanionOf == null ||
                    !objectManager.TryGetIdWithLogging(oneToOneConversationHero.CompanionOf, out var currentClanId) ||
                    currentClanId != data.ExpectedClanId)
                    throw new InvalidOperationException("The companion's owning clan changed before dismissal.");

                string currentPartyId = null;
                if (oneToOneConversationHero.PartyBelongedTo != null &&
                    !objectManager.TryGetIdWithLogging(oneToOneConversationHero.PartyBelongedTo, out currentPartyId))
                    throw new InvalidOperationException("The companion's current party could not be resolved.");
                if (currentPartyId != data.ExpectedPartyId)
                    throw new InvalidOperationException("The companion's party changed before dismissal.");

                TroopRoster memberRoster = oneToOneConversationHero.PartyBelongedTo?.MemberRoster;
                string memberRosterId = null;
                string characterId = null;
                if (memberRoster != null)
                {
                    if (!objectManager.TryGetIdWithLogging(memberRoster, out memberRosterId) ||
                        !objectManager.TryGetIdWithLogging(oneToOneConversationHero.CharacterObject, out characterId))
                        throw new InvalidOperationException("The companion's party roster could not be resolved.");
                    memberRosterId = Compact(memberRosterId, typeof(TroopRoster));
                    characterId = Compact(characterId, typeof(CharacterObject));
                }

                RemoveCompanionAction.ApplyByFire(oneToOneConversationHero.CompanionOf, oneToOneConversationHero);
                try
                {
                    KillCharacterAction.ApplyByRemove(oneToOneConversationHero, false, true);
                }
                finally
                {
                    // Once RemoveCompanionAction clears CompanionOf, a retry cannot rediscover the old party.
                    // Always finish the captured roster correction, even if the follow-up removal throws.
                    if (memberRoster != null)
                    {
                        ReconcileDismissedCompanionRoster(memberRoster, oneToOneConversationHero.CharacterObject,
                            memberRosterId, characterId, network, sendCoalescer);
                    }
                    else
                        sendCoalescer?.Flush(network);
                }

                success = true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                logger.Error(exception, "Failed companion dismissal request {RequestId} for {HeroId}",
                    data.RequestId, data.OneToOneConversationHeroId);
            }
            finally
            {
                SendCompletion(requester, data.RequestId, data.OneToOneConversationHeroId, success, error);
            }
        }, context: nameof(FireCompanion));
    }

    private void Handle_FireCompanionCompleted(MessagePayload<FireCompanionCompleted> obj)
    {
        var data = obj.What;
        logger.Information(
            "Received companion dismissal completion {RequestId} for {HeroId}; pending request {PendingRequestId} for {PendingHeroId}; game-thread queue {QueueDepth}",
            data.RequestId, data.OneToOneConversationHeroId, pendingFireCompanionRequestId,
            pendingFireCompanionHeroId, GameThread.Instance.QueueLength);

        // Roster corrections and this acknowledgement share the reliable ordered stream. Deferring all of
        // them to the game thread preserves that FIFO before the player can open the party screen.
        GameThread.RunSafe(() =>
        {
            CompletePendingFireCompanion(data.RequestId, data.OneToOneConversationHeroId,
                data.Success, data.Error);
        }, context: nameof(FireCompanionCompleted));
    }

    private void CompletePendingFireCompanion(string requestId, string heroId, bool success, string error)
    {
        if (requestId != pendingFireCompanionRequestId || heroId != pendingFireCompanionHeroId)
        {
            logger.Warning("Ignored unmatched companion dismissal completion {RequestId} for {HeroId}",
                requestId, heroId);
            return;
        }

        pendingFireCompanionRequestId = null;
        pendingFireCompanionHeroId = null;

        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.LeaveEncounter = true;
        }

        if (!success)
        {
            logger.Error("Companion dismissal request {RequestId} failed: {Error}", requestId, error);
        }

        messageBroker.Publish(this, new CompanionDismissalCompleted(requestId, heroId, success, error));
    }

    private void SendCompletion(NetPeer requester, string requestId, string heroId, bool success, string error)
    {
        logger.Information(
            "Sending immediate companion dismissal completion {RequestId} for {HeroId} to peer {PeerId}; success {Success}",
            requestId, heroId, requester.Id, success);

        // SendImmediate first flushes every message already buffered for this peer and then writes the
        // completion directly on the same reliable ordered stream. The acknowledgement therefore cannot
        // remain in the network aggregation buffer after the roster corrections it confirms.
        network.SendImmediate(requester, new FireCompanionCompleted(requestId, heroId, success, error));
    }

    internal static void ReconcileDismissedCompanionRoster(TroopRoster memberRoster, CharacterObject character,
        string memberRosterId, string characterId, INetwork network, ISendCoalescer sendCoalescer = null)
    {
        int index = memberRoster.FindIndexOfTroop(character);
        if (index >= 0)
        {
            var element = memberRoster.GetElementCopyAtIndex(index);
            if (element.Number != 0 || element.WoundedNumber != 0)
            {
                memberRoster.AddToCounts(character, -element.Number, false, -element.WoundedNumber,
                    0, true);
            }
        }

        memberRoster.RemoveZeroCounts();

        // Flush the ordinary coalesced deltas before the absolute correction and correlated completion.
        // All of them share the reliable ordered stream, so the client cannot observe the acknowledgement
        // while a stale roster delta from this dismissal is still pending for the next server tick.
        sendCoalescer?.Flush(network);

        // Send an absolute correction after the ordinary deltas. This is idempotent and repairs clients
        // that entered the dismissal with duplicate companion counts.
        network.SendAll(new NetworkTroopRosterSetWoundedNumber(memberRosterId, characterId, 0));
        network.SendAll(new NetworkTroopRosterSetNumber(memberRosterId, characterId, 0));
        network.SendAll(new NetworkTroopRosterRemoveZeroCounts(memberRosterId));
    }

    private void Handle_CompanionRejoinAfterEmprisonment(MessagePayload<CompanionRejoinAfterEmprisonment> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero, out var oneToOneConversationHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;

        var message = new DoCompanionRejoinAfterEmprisonment(
            oneToOneConversationHeroId,
            mainPartyId
        );

        network.SendAll(message);
    }

    private void Handle_DoCompanionRejoinAfterEmprisonment(MessagePayload<DoCompanionRejoinAfterEmprisonment> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OneToOneConversationHeroId, out var oneToOneConversationHero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyId, out var mainParty)) return;

            AddHeroToPartyAction.Apply(oneToOneConversationHero, mainParty, true);
        });
    }

    private void Handle_CompanionJoinedPartyByRescue(MessagePayload<CompanionJoinedPartyByRescue> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero, out var oneToOneConversationHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.MainParty, out var mainPartyId)) return;

        var message = new DoCompanionJoinedPartyByRescue(
            oneToOneConversationHeroId,
            mainPartyId
        );

        network.SendAll(message);
    }

    private void Handle_DoCompanionJoinedPartyByRescue(MessagePayload<DoCompanionJoinedPartyByRescue> obj)
    {
        var data = obj.What;
        var requester = obj.Who as NetPeer;

        if (requester == null)
        {
            logger.Error("Rejected {Message} without a requesting peer",
                nameof(DoCompanionJoinedPartyByRescue));
            return;
        }

        GameThread.RunSafe(() =>
        {
            var status = CompanionRescueCompletionStatus.Rejected;
            string error = null;
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.OneToOneConversationHeroId,
                    out var companion))
                    throw new InvalidOperationException("The requested companion could not be resolved.");
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyId,
                    out var targetParty))
                    throw new InvalidOperationException("The requested target party could not be resolved.");

                var companionClan = ValidateRescueOwnership(companion, targetParty);
                ValidateRescueRequester(requester, companionClan, targetParty);
                int targetCount = targetParty.MemberRoster.GetTroopCount(companion.CharacterObject);
                if (!companion.IsPrisoner && companion.PartyBelongedTo == targetParty &&
                    companion.HeroState == Hero.CharacterStates.Active && targetCount == 1)
                {
                    status = CompanionRescueCompletionStatus.AlreadyCompleted;
                }
                else
                {
                    ValidateCaptiveRescueState(companion, targetCount);

                    EndCaptivityAction.ApplyByReleasedAfterBattle(companion);
                    companion.ChangeState(Hero.CharacterStates.Active);
                    if (targetParty.MemberRoster.GetTroopCount(companion.CharacterObject) == 0)
                        targetParty.AddElementToMemberRoster(companion.CharacterObject, 1, false);

                    if (companion.IsPrisoner || companion.PartyBelongedTo != targetParty ||
                        targetParty.MemberRoster.GetTroopCount(companion.CharacterObject) != 1)
                        throw new InvalidOperationException("The join-party rescue did not reach its terminal state.");

                    status = CompanionRescueCompletionStatus.Accepted;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                logger.Error(exception, "Rejected companion join rescue for {HeroId}",
                    data.OneToOneConversationHeroId);
            }
            finally
            {
                CompleteRescueRequest(requester, data.OneToOneConversationHeroId,
                    CompanionRescueRequestKind.JoinParty, status, error);
            }
        }, context: nameof(DoCompanionJoinedPartyByRescue));
    }

    private void Handle_PartyScreenClosedFromRescuing(MessagePayload<PartyScreenClosedFromRescuing> obj)
    {
        var companionElements = obj.What.LeftMemberRoster.GetTroopRoster()
            .Where(element => element.Number > 0 &&
                element.Character?.HeroObject?.CompanionOf != null)
            .ToArray();
        if (companionElements.Length != 1)
        {
            logger.Error("Rejected companion rescue party screen with {Count} companion elements",
                companionElements.Length);
            return;
        }

        var companion = companionElements[0].Character.HeroObject;
        // These rosters are not registered yet, send data instead of ids
        var leftMemberData = troopRosterInterface.PackTroopRosterData(obj.What.LeftMemberRoster);
        var leftPrisonerData = troopRosterInterface.PackTroopRosterData(obj.What.LeftPrisonRoster);

        if (!objectManager.TryGetIdWithLogging(obj.What.RightOwnerParty, out var rightOwnerPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(companion, out var companionHeroId)) return;

        var message = new DoPartyScreenClosedFromRescuing(
            leftMemberData,
            leftPrisonerData,
            rightOwnerPartyId,
            companionHeroId
        );

        network.SendAll(message);
    }

    private void Handle_DoPartyScreenClosedFromRescuing(MessagePayload<DoPartyScreenClosedFromRescuing> obj)
    {
        var data = obj.What;
        var requester = obj.Who as NetPeer;

        if (requester == null)
        {
            logger.Error("Rejected {Message} without a requesting peer",
                nameof(DoPartyScreenClosedFromRescuing));
            return;
        }

        GameThread.RunSafe(() =>
        {
            var status = CompanionRescueCompletionStatus.Rejected;
            string error = null;
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.CompanionHeroId,
                    out var companion))
                    throw new InvalidOperationException("The requested companion could not be resolved.");
                if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.RightOwnerPartyId,
                    out var rightOwnerParty) || rightOwnerParty.MobileParty == null)
                    throw new InvalidOperationException("The requested target party could not be resolved.");

                var targetParty = rightOwnerParty.MobileParty;
                var companionClan = ValidateRescueOwnership(companion, targetParty);
                ValidateRescueRequester(requester, companionClan, targetParty);
                var leftMemberElements = troopRosterInterface
                    .UnpackTroopRosterData(data.LeftMemberRosterData).ToArray();
                var leftPrisonerElements = troopRosterInterface
                    .UnpackTroopRosterData(data.LeftPrisonRosterData).ToArray();
                var rescuedElements = leftMemberElements
                    .Where(element => element.Character == companion.CharacterObject)
                    .ToArray();
                if (rescuedElements.Length != 1 || rescuedElements[0].Number != 1)
                    throw new InvalidOperationException("The rescue party roster does not contain exactly one requested companion.");
                if (leftMemberElements.Any(element => element.Number > 0 &&
                    element.Character?.HeroObject != null && element.Character.HeroObject != companion))
                    throw new InvalidOperationException("The rescue party roster contains another hero.");

                int targetCount = targetParty.MemberRoster.GetTroopCount(companion.CharacterObject);
                var existingParties = FindCompanionLedParties(companionClan, companion);
                if (!companion.IsPrisoner && companion.HeroState == Hero.CharacterStates.Active &&
                    targetCount == 0 && existingParties.Length == 1 &&
                    companion.PartyBelongedTo == existingParties[0])
                {
                    status = CompanionRescueCompletionStatus.AlreadyCompleted;
                }
                else
                {
                    if (existingParties.Length != 0)
                        throw new InvalidOperationException("The companion already has a clan war party in a non-terminal state.");
                    ValidateCaptiveRescueState(companion, targetCount);
                    int originalWarPartyCount = companionClan.WarPartyComponents.Count;

                    companionRolesCampaignBehaviorInterface.PartyScreenClosed(
                        companion,
                        leftMemberElements,
                        leftPrisonerElements,
                        rightOwnerParty,
                        false
                    );

                    var createdParties = FindCompanionLedParties(companionClan, companion);
                    if (companion.IsPrisoner || companion.HeroState != Hero.CharacterStates.Active ||
                        targetParty.MemberRoster.GetTroopCount(companion.CharacterObject) != 0 ||
                        createdParties.Length != 1 || companion.PartyBelongedTo != createdParties[0] ||
                        companionClan.WarPartyComponents.Count != originalWarPartyCount + 1)
                        throw new InvalidOperationException("The lead-party rescue did not reach its terminal state.");

                    status = CompanionRescueCompletionStatus.Accepted;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                logger.Error(exception, "Rejected companion lead-party rescue for {HeroId}",
                    data.CompanionHeroId);
            }
            finally
            {
                CompleteRescueRequest(requester, data.CompanionHeroId,
                    CompanionRescueRequestKind.LeadParty, status, error);
            }
        }, context: nameof(DoPartyScreenClosedFromRescuing));
    }

    private void Handle_CompanionRescueCompleted(MessagePayload<CompanionRescueCompleted> obj)
    {
        var data = obj.What;
        GameThread.RunSafe(() =>
        {
            if (data.Status == CompanionRescueCompletionStatus.Rejected)
            {
                logger.Error("Companion rescue for {HeroId} was rejected: {Error}",
                    data.CompanionHeroId, data.Error);
            }

            messageBroker.Publish(this, new CompanionRescueCompletionReceived(
                data.CompanionHeroId,
                data.Kind,
                data.Status,
                data.Error));
        }, context: nameof(CompanionRescueCompleted));
    }

    private static Clan ValidateRescueOwnership(Hero companion, MobileParty targetParty)
    {
        var companionClan = companion.CompanionOf;
        if (companionClan == null)
            throw new InvalidOperationException("The companion has no owning clan.");
        if (targetParty.ActualClan != companionClan || targetParty.LeaderHero?.Clan != companionClan)
            throw new InvalidOperationException("The target party no longer belongs to the companion's clan.");
        return companionClan;
    }

    private void ValidateRescueRequester(NetPeer requester, Clan companionClan,
        MobileParty targetParty)
    {
        if (!playerManager.TryGetPlayer(requester, out var player))
            throw new InvalidOperationException("The requesting peer has no registered player.");
        if (string.IsNullOrWhiteSpace(player.ClanId) ||
            !objectManager.TryGetObjectWithLogging<Clan>(player.ClanId, out var playerClan) ||
            playerClan != companionClan)
            throw new InvalidOperationException("The requesting player does not own the companion's clan.");
        if (string.IsNullOrWhiteSpace(player.MobilePartyId) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty) ||
            playerParty != targetParty)
            throw new InvalidOperationException("The requesting player does not own the target party.");
    }

    private static void ValidateCaptiveRescueState(Hero companion, int targetCount)
    {
        if (targetCount != 0)
            throw new InvalidOperationException("The target roster already contains the companion in a non-terminal state.");
        if (!companion.IsPrisoner || companion.PartyBelongedToAsPrisoner == null)
            throw new InvalidOperationException("The companion is no longer captive and the rescue is not completed.");
        if (companion.PartyBelongedTo != null)
            throw new InvalidOperationException("The captive companion still belongs to an active party.");
    }

    private static MobileParty[] FindCompanionLedParties(Clan clan, Hero companion)
    {
        return clan.WarPartyComponents
            .Select(component => component?.MobileParty)
            .Where(party => party != null &&
                (party.LeaderHero == companion ||
                 party.StringId == companion.CharacterObject.StringId))
            .Distinct()
            .ToArray();
    }

    private void CompleteRescueRequest(NetPeer requester, string companionHeroId,
        CompanionRescueRequestKind kind, CompanionRescueCompletionStatus status, string error)
    {
        var completion = new CompanionRescueCompleted(
            companionHeroId, kind, status, error);
        SendRescueCompletion(requester, completion);
    }

    private void SendRescueCompletion(NetPeer requester, CompanionRescueCompleted completion)
    {
        sendCoalescer?.Flush(network);
        network.SendImmediate(requester, completion);
    }

    private void Handle_CompanionRescued(MessagePayload<CompanionRescued> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.OneToOneConversationHero, out var oneToOneConversationHeroId)) return;

        var message = new RescueCompanion(oneToOneConversationHeroId);

        network.SendAll(message);
    }

    private void Handle_RescueCompanion(MessagePayload<RescueCompanion> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OneToOneConversationHeroId, out var oneToOneConversationHero)) return;
            if (!oneToOneConversationHero.IsPrisoner) return;

            EndCaptivityAction.ApplyByReleasedAfterBattle(oneToOneConversationHero);
        }, context: nameof(RescueCompanion));
    }

}
