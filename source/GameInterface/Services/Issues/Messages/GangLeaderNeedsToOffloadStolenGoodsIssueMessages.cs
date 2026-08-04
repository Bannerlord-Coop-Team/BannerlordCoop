using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

// --- Creation: server rolls once (one dice roll + a deterministically-picked hideout + an AfterIssueCreation-
// derived CounterOfferHero), broadcasts the resolved terms. Finalize/removal rides the fully generic
// VillageNeedsToolsIssueHandler/VillageNeedsToolsIssueInterface machinery unchanged once this type is
// allowlisted (see IGangLeaderNeedsToOffloadStolenGoodsIssueInterface's doc comment) - no bespoke messages
// needed for that. Accept-race REJECTION also reuses the shared NetworkVillageIssueAcceptRejected wire type and
// VillageNeedsToolsIssueHandler's already-generic handler for it (see
// Handlers.GangLeaderNeedsToOffloadStolenGoodsIssueHandler's doc comment) - no bespoke reject message needed
// either. Quest-solution AND alternative-solution accept DO need their own bespoke messages: both re-derive
// per-client-divergent price/reward terms at accept time (see the interface's doc comment for the full
// derivation). ---

/// <summary>Published on the server after a genuine <c>IssueManager.CreateNewIssue</c> creates a
/// <see cref="GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue"/>,
/// carrying the live issue so <see cref="Handlers.GangLeaderNeedsToOffloadStolenGoodsIssueHandler"/> can capture
/// its creation-time-rolled/derived fields.</summary>
public readonly struct GangLeaderStolenGoodsIssueCreated : IEvent
{
    public readonly GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue Issue;

    public GangLeaderStolenGoodsIssueCreated(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Server -> all clients: the authoritatively-picked hideout, the server's own
/// <c>_randomForStolenTradeGood</c> roll, and the <c>AfterIssueCreation</c>-derived counter-offer hero for a
/// newly created Gang Leader Needs to Offload Stolen Goods issue, so every peer constructs a byte-identical
/// mirror instead of independently re-rolling or re-deriving any of the three.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangLeaderStolenGoodsIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string IssueHideoutId;
    [ProtoMember(3)]
    public readonly int RandomForStolenTradeGood;
    [ProtoMember(4)]
    public readonly string CounterOfferHeroId;

    public NetworkGangLeaderStolenGoodsIssueCreated(
        string ownerId, string issueHideoutId, int randomForStolenTradeGood, string counterOfferHeroId)
    {
        OwnerId = ownerId;
        IssueHideoutId = issueHideoutId;
        RandomForStolenTradeGood = randomForStolenTradeGood;
        CounterOfferHeroId = counterOfferHeroId;
    }
}

/// <summary>Published locally whenever <c>IssueManager.StartIssueQuest</c> genuinely runs for this issue type
/// (the accepting player's own live conversation, on whichever machine that is), carrying what THAT machine's
/// own real ctor call just baked - see <see cref="IGangLeaderNeedsToOffloadStolenGoodsIssueInterface"/>'s doc
/// comment for why none of this can be trusted as authoritative until the server arbitrates it.</summary>
public readonly struct GangLeaderStolenGoodsQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly int StolenTradeGoodAmount;
    public readonly int StolenTradeGoodPrice;
    public readonly int RewardGold;
    public readonly int CounterOfferGold;

    public GangLeaderStolenGoodsQuestAcceptTriggered(
        Hero owner, string controllerId, int stolenTradeGoodAmount, int stolenTradeGoodPrice, int rewardGold, int counterOfferGold)
    {
        Owner = owner;
        ControllerId = controllerId;
        StolenTradeGoodAmount = stolenTradeGoodAmount;
        StolenTradeGoodPrice = stolenTradeGoodPrice;
        RewardGold = rewardGold;
        CounterOfferGold = counterOfferGold;
    }
}

/// <summary>Client -> server: "hero X's issue giver just accepted the quest solution in my live
/// conversation." Deliberately its own type (not <c>RequestVillageIssueAcceptQuest</c>) - see
/// Messages/VillageNeedsCraftingMaterialsIssueMessages.cs's file-level note for why sharing a wire type across
/// issue types that need different accept handling risks a bogus cross-handler rejection.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestGangLeaderStolenGoodsAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestGangLeaderStolenGoodsAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -> all clients: confirms the quest-solution accept, carrying the authoritative price/reward
/// terms every peer (including the original accepter) force-corrects its own quest object to.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangLeaderStolenGoodsQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly int StolenTradeGoodAmount;
    [ProtoMember(4)]
    public readonly int StolenTradeGoodPrice;
    [ProtoMember(5)]
    public readonly int RewardGold;
    [ProtoMember(6)]
    public readonly int CounterOfferGold;

    public NetworkGangLeaderStolenGoodsQuestAccepted(
        string ownerId, string ownerControllerId, int stolenTradeGoodAmount, int stolenTradeGoodPrice, int rewardGold, int counterOfferGold)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        StolenTradeGoodAmount = stolenTradeGoodAmount;
        StolenTradeGoodPrice = stolenTradeGoodPrice;
        RewardGold = rewardGold;
        CounterOfferGold = counterOfferGold;
    }
}

/// <summary>Published locally whenever <c>IssueBase.StartIssueWithAlternativeSolution</c> genuinely runs for
/// this issue type, carrying the Issue-level <c>StolenTradeGoodAmount</c>/<c>RewardGold</c> THAT machine's own
/// live properties just computed - the values to freeze against later <c>Town.GetItemPrice</c> drift (see the
/// interface's doc comment).</summary>
public readonly struct GangLeaderStolenGoodsAlternativeAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly int StolenTradeGoodAmount;
    public readonly int RewardGold;

    public GangLeaderStolenGoodsAlternativeAcceptTriggered(Hero owner, string controllerId, int stolenTradeGoodAmount, int rewardGold)
    {
        Owner = owner;
        ControllerId = controllerId;
        StolenTradeGoodAmount = stolenTradeGoodAmount;
        RewardGold = rewardGold;
    }
}

/// <summary>Client -> server equivalent of <see cref="RequestGangLeaderStolenGoodsAcceptQuest"/> for the
/// alternative-solution accept option. Unlike the quest-solution request, this DOES carry the requesting
/// client's own captured <see cref="StolenTradeGoodAmount"/>/<see cref="RewardGold"/>: there is no server-side
/// replay for the alternative-solution path at all (see
/// <see cref="Patches.IssueWithAlternativeSolutionAcceptancePatch"/>'s own doc comment on why
/// <c>StartIssueWithAlternativeSolution</c> is never replayed), so the server has no ALTERNATIVE authoritative
/// source for these values - it must trust whichever peer's request reaches it first, exactly like the
/// existing generic alternative-solution mirror already does for every other issue type (no re-derivation
/// possible, nothing to arbitrate against). Bug found and fixed during this pass: the server's own handler
/// initially re-captured these from ITS OWN (stale - never having run its own StartIssueWithAlternativeSolution)
/// issue object instead of trusting the request, reintroducing the exact "capture from the genuine accepter,
/// not somewhere else" bug this whole feature exists to prevent.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestGangLeaderStolenGoodsAcceptAlternative : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly int StolenTradeGoodAmount;
    [ProtoMember(3)]
    public readonly int RewardGold;

    public RequestGangLeaderStolenGoodsAcceptAlternative(string ownerId, int stolenTradeGoodAmount, int rewardGold)
    {
        OwnerId = ownerId;
        StolenTradeGoodAmount = stolenTradeGoodAmount;
        RewardGold = rewardGold;
    }
}

/// <summary>Server -> all clients equivalent of <see cref="NetworkGangLeaderStolenGoodsQuestAccepted"/> for the
/// alternative-solution accept option, carrying the frozen payout amounts.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangLeaderStolenGoodsAlternativeAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly int StolenTradeGoodAmount;
    [ProtoMember(4)]
    public readonly int RewardGold;

    public NetworkGangLeaderStolenGoodsAlternativeAccepted(
        string ownerId, string ownerControllerId, int stolenTradeGoodAmount, int rewardGold)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        StolenTradeGoodAmount = stolenTradeGoodAmount;
        RewardGold = rewardGold;
    }
}
