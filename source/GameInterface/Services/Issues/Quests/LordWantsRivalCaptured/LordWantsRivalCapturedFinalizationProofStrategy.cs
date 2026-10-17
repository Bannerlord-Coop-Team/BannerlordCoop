using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordWantsRivalCaptured;

/// <summary>
/// Runs the owner's prisoner dialog consequences on the server as that player, after checking the prisoner
/// and the receiver against what the server sees.
/// </summary>
internal class LordWantsRivalCapturedFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordWantsRivalCapturedFinalizationProofStrategy>();

    private const int DeliveredTargetRelation = -10;
    private const int DeliveredGiverRelation = 15;
    private const float DeliveredRenown = 6f;

    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ConversationPartyTracker conversationPartyTracker;

    public LordWantsRivalCapturedFinalizationProofStrategy(
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ConversationPartyTracker conversationPartyTracker)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.conversationPartyTracker = conversationPartyTracker;
    }

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest rivalQuest))
        {
            return false;
        }

        switch ((LordWantsRivalCapturedBranch)proof)
        {
            case LordWantsRivalCapturedBranch.CapturedAtCounterOffer:
                CaptureAtCounterOffer(rivalQuest);
                return true;
            case LordWantsRivalCapturedBranch.CounterOfferAccepted:
                AcceptCounterOffer(rivalQuest);
                return true;
            case LordWantsRivalCapturedBranch.DeliveredToGiver:
                Deliver(rivalQuest, GiverReceiver(rivalQuest));
                return true;
            case LordWantsRivalCapturedBranch.DeliveredToGuard:
                Deliver(rivalQuest, GuardReceiver(rivalQuest));
                return true;
            default:
                return false;
        }
    }

    private static void CaptureAtCounterOffer(Quest quest)
    {
        var target = quest._targetHero;

        // The battle loot can already have made the target the owner's prisoner
        if (target.PartyBelongedToAsPrisoner == PartyBase.MainParty)
        {
            quest._firstCounterOfferMade = true;
            return;
        }

        if (target.IsPrisoner || !target.IsAlive)
        {
            Logger.Warning("The target of {quest} cannot be taken prisoner by its owner", quest.StringId);
            return;
        }

        quest.FirstCounterOfferFinished();
    }

    private static void AcceptCounterOffer(Quest quest)
    {
        if (!quest.PlayerCapturedTargetHero)
        {
            Logger.Warning("The owner of {quest} does not hold its target, not releasing it for the counter offer", quest.StringId);
            return;
        }

        quest.QuestFailCounterOfferAccepted();
    }

    // Vanilla's PlayerDeliveredPrisonerQuestSuccess picks the receiver from the open conversation and Settlement.CurrentSettlement, which the server does not have
    private static void Deliver(Quest quest, PartyBase receiver)
    {
        if (receiver == null || !quest.PlayerCapturedTargetHero)
        {
            Logger.Warning("The owner of {quest} cannot deliver its target here", quest.StringId);
            return;
        }

        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, quest.RewardGold);
        ChangeRelationAction.ApplyPlayerRelation(quest._targetHero, DeliveredTargetRelation);
        quest.RelationshipChangeWithQuestGiver = DeliveredGiverRelation;
        GainRenownAction.Apply(Hero.MainHero, DeliveredRenown);
        quest.CompleteQuestWithSuccess();
        TransferPrisonerAction.Apply(quest._targetHero.CharacterObject, PartyBase.MainParty, receiver);
    }

    private PartyBase GiverReceiver(Quest quest)
    {
        var giver = quest.QuestGiver;
        var inGiversSettlement = giver.CurrentSettlement != null && MobileParty.MainParty.CurrentSettlement == giver.CurrentSettlement;

        if (!inGiversSettlement && !IsTalkingTo(giver.PartyBelongedTo))
        {
            return null;
        }

        return giver.PartyBelongedTo?.Party ?? giver.CurrentSettlement.Party;
    }

    // Vanilla sends the giver's guard when the owner enters a fortification of the giver's clan
    private static PartyBase GuardReceiver(Quest quest)
    {
        var settlement = MobileParty.MainParty.CurrentSettlement;

        if (settlement == null || !settlement.IsFortification || settlement.OwnerClan != quest.QuestGiver.Clan)
        {
            return null;
        }

        return settlement.Party;
    }

    private bool IsTalkingTo(MobileParty giverParty)
    {
        if (giverParty == null ||
            !objectManager.TryGetId(Hero.MainHero, out var ownerHeroId) ||
            !objectManager.TryGetId(giverParty.Party, out var giverPartyId) ||
            !objectManager.TryGetId(MobileParty.MainParty.Party, out var ownerPartyId))
        {
            return false;
        }

        var owner = playerManager.Players.FirstOrDefault(player => player.HeroId == ownerHeroId);

        return owner != null &&
            playerManager.TryGetPeer(owner.ControllerId, out var peer) &&
            conversationPartyTracker.TryGetEngagement(peer, out var engagement) &&
            engagement.PartyId == giverPartyId &&
            engagement.EngagerPartyId == ownerPartyId;
    }
}
