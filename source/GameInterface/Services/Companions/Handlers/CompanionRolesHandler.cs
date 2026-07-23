using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using LiteNetLib;
using Serilog;
using System;
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
    private readonly ISendCoalescer sendCoalescer;
    private string pendingFireCompanionRequestId;
    private string pendingFireCompanionHeroId;

    public CompanionRolesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
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
        network.Send(requester, new FireCompanionCompleted(requestId, heroId, success, error));
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

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.OneToOneConversationHeroId, out var oneToOneConversationHero)) return;
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyId, out var mainParty)) return;

                AddHeroToPartyAction.Apply(oneToOneConversationHero, mainParty, true);
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(DoCompanionRejoinAfterEmprisonment));
            }
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

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.OneToOneConversationHeroId, out var oneToOneConversationHero)) return;
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyId, out var mainParty)) return;

                EndCaptivityAction.ApplyByReleasedAfterBattle(oneToOneConversationHero);
                oneToOneConversationHero.ChangeState(Hero.CharacterStates.Active);
                mainParty.AddElementToMemberRoster(oneToOneConversationHero.CharacterObject, 1, false);
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(DoCompanionJoinedPartyByRescue));
            }
        });
    }

    private void Handle_PartyScreenClosedFromRescuing(MessagePayload<PartyScreenClosedFromRescuing> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.LeftOwnerParty, out var leftOwnerPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.LeftMemberRoster, out var leftMemberRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.LeftPrisonRoster, out var leftPrisonRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.RightOwnerParty, out var rightOwnerPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.RightMemberRoster, out var rightMemberRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.RightPrisonRoster, out var rightPrisonRosterId)) return;

        var message = new DoPartyScreenClosedFromRescuing(
            leftOwnerPartyId,
            leftMemberRosterId,
            leftPrisonRosterId,
            rightOwnerPartyId,
            rightMemberRosterId,
            rightPrisonRosterId
        );

        network.SendAll(message);
    }

    private void Handle_DoPartyScreenClosedFromRescuing(MessagePayload<DoPartyScreenClosedFromRescuing> obj)
    {
        var data = obj.What;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.LeftOwnerPartyId, out var leftOwnerParty)) return;
                if (!objectManager.TryGetObjectWithLogging<TroopRoster>(data.LeftMemberRosterId, out var leftMemberRoster)) return;
                if (!objectManager.TryGetObjectWithLogging<TroopRoster>(data.LeftPrisonRosterId, out var leftPrisonRoster)) return;
                if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.RightOwnerPartyId, out var rightOwnerParty)) return;
                if (!objectManager.TryGetObjectWithLogging<TroopRoster>(data.RightMemberRosterId, out var rightMemberRoster)) return;
                if (!objectManager.TryGetObjectWithLogging<TroopRoster>(data.RightPrisonRosterId, out var rightPrisonRoster)) return;

                var companionRolesCampaignBehavior = Campaign.Current.GetCampaignBehavior<CompanionRolesCampaignBehavior>();

                companionRolesCampaignBehavior.PartyScreenClosed(
                    leftOwnerParty,
                    leftMemberRoster,
                    leftPrisonRoster,
                    rightOwnerParty,
                    rightMemberRoster,
                    rightPrisonRoster,
                    false
                );
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(DoPartyScreenClosedFromRescuing));
            }
        });
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

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(data.OneToOneConversationHeroId, out var oneToOneConversationHero)) return;

                EndCaptivityAction.ApplyByReleasedAfterBattle(oneToOneConversationHero);
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(RescueCompanion));
            }
        });
    }
}
