using Common.Logging;
using Common;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
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
    private readonly IIssueOwnershipRegistry ownership;
    private readonly ConversationPartyTracker conversationPartyTracker;
    private readonly Dictionary<Quest, CaptureEntitlement> pendingCaptures = new();

    public LordWantsRivalCapturedFinalizationProofStrategy(
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IIssueOwnershipRegistry ownership,
        ConversationPartyTracker conversationPartyTracker)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.ownership = ownership;
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

    internal void RecordPendingCaptures(Player player, TroopRosterData prisoners)
    {
        if (ModInformation.IsClient) return;

        ClearPlayerPendingCaptures(player.ControllerId);
        if (prisoners.Data == null) return;

        var issues = Campaign.Current.IssueManager.Issues.Values
            .Concat(Hero.AllAliveHeroes.Select(hero => hero.Issue))
            .Where(issue => issue != null && issue.IssueQuest is Quest)
            .Distinct();

        foreach (var issue in issues)
        {
            var quest = (Quest)issue.IssueQuest;
            if (!quest.IsOngoing ||
                !objectManager.TryGetId(issue.IssueOwner, out var issueOwnerId) ||
                !ownership.TryGetOwner(issueOwnerId, issue.StringId, out var ownerControllerId) ||
                ownerControllerId != player.ControllerId ||
                !objectManager.TryGetHandleWithLogging(quest._targetHero.CharacterObject, out var targetHandle) ||
                !prisoners.Data.Any(troop => troop.CharacterId == targetHandle && troop.Number > 0))
            {
                continue;
            }

            // The quest object is the framework's generation token. The dialogue may outlive the battle.
            pendingCaptures[quest] = new CaptureEntitlement(
                quest,
                issue.StringId,
                quest._targetHero,
                player.ControllerId,
                player.HeroId,
                player.MobilePartyId);
        }
    }

    internal void ClearPendingCapture(Quest quest)
    {
        if (quest != null) pendingCaptures.Remove(quest);
    }

    internal void ClearTargetPendingCaptures(Hero target)
    {
        foreach (var entry in pendingCaptures
            .Where(entry => entry.Value.Target == target)
            .Select(entry => entry.Key)
            .ToArray())
        {
            pendingCaptures.Remove(entry);
        }
    }

    internal void ClearPlayerPendingCaptures(string controllerId)
    {
        foreach (var entry in pendingCaptures
            .Where(entry => entry.Value.ControllerId == controllerId)
            .Select(entry => entry.Key)
            .ToArray())
        {
            pendingCaptures.Remove(entry);
        }
    }

    private void CaptureAtCounterOffer(Quest quest)
    {
        var target = quest._targetHero;

        // The battle loot can already have made the target the owner's prisoner
        if (target.PartyBelongedToAsPrisoner == PartyBase.MainParty)
        {
            ClearPendingCapture(quest);
            quest._firstCounterOfferMade = true;
            return;
        }

        if (target.IsPrisoner || !target.IsAlive)
        {
            Logger.Warning("The target of {quest} cannot be taken prisoner by its owner", quest.StringId);
            return;
        }

        if (!TryConsumePendingCapture(quest))
        {
            Logger.Warning("The owner of {quest} has no allocated capture for its free target", quest.StringId);
            return;
        }

        quest.FirstCounterOfferFinished();
    }

    private bool TryConsumePendingCapture(Quest quest)
    {
        var issueId = IssueManager.GetIssueOfQuest(quest)?.StringId;
        if (!pendingCaptures.TryGetValue(quest, out var pending) ||
            pending.Quest != quest ||
            pending.IssueId != issueId ||
            pending.Target != quest._targetHero ||
            !objectManager.TryGetId(Hero.MainHero, out var heroId) ||
            !objectManager.TryGetId(MobileParty.MainParty, out var partyId) ||
            pending.HeroId != heroId ||
            pending.MobilePartyId != partyId ||
            !playerManager.Players.Any(player =>
                player.ControllerId == pending.ControllerId &&
                player.HeroId == heroId &&
                player.MobilePartyId == partyId))
        {
            return false;
        }

        pendingCaptures.Remove(quest);
        return true;
    }

    private readonly struct CaptureEntitlement
    {
        public CaptureEntitlement(
            Quest quest,
            string issueId,
            Hero target,
            string controllerId,
            string heroId,
            string mobilePartyId)
        {
            Quest = quest;
            IssueId = issueId;
            Target = target;
            ControllerId = controllerId;
            HeroId = heroId;
            MobilePartyId = mobilePartyId;
        }

        public Quest Quest { get; }
        public string IssueId { get; }
        public Hero Target { get; }
        public string ControllerId { get; }
        public string HeroId { get; }
        public string MobilePartyId { get; }
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
