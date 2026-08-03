using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

/// <summary>Published from <see cref="Patches.LandlordTrainingForRetainersAcceptancePatches"/> whenever
/// <c>IssueManager.StartIssueQuest</c> genuinely runs for a Landlord Training for Retainers issue. Carries the
/// accepting machine's captured <c>_difficultyMultiplier</c>/<c>RewardGold</c> - see
/// <see cref="Interfaces.ILandlordTrainingForRetainersIssueInterface"/>'s doc comment for why both are
/// genuinely per-client at accept time and must be force-corrected on every other peer.</summary>
public readonly struct LandlordTrainingIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly float DifficultyMultiplier;
    public readonly int RewardGold;

    public LandlordTrainingIssueQuestAcceptTriggered(Hero owner, string controllerId, float difficultyMultiplier, int rewardGold)
    {
        Owner = owner;
        ControllerId = controllerId;
        DifficultyMultiplier = difficultyMultiplier;
        RewardGold = rewardGold;
    }
}

/// <summary>Client -&gt; server: "hero X's issue giver just accepted the quest solution in my live
/// conversation." Own type (not reused from Village Needs Tools' identically-shaped message) for the same
/// cross-handler-ambiguity reason documented on Village Needs Crafting Materials' equivalent messages.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestLandlordTrainingIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestLandlordTrainingIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -&gt; all clients: confirms the quest-solution accept, carrying the authoritative
/// <see cref="DifficultyMultiplier"/>/<see cref="RewardGold"/> every peer (including the original accepter)
/// force-corrects its own quest object to.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLandlordTrainingIssueQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly float DifficultyMultiplier;
    [ProtoMember(4)]
    public readonly int RewardGold;

    public NetworkLandlordTrainingIssueQuestAccepted(string ownerId, string ownerControllerId, float difficultyMultiplier, int rewardGold)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        DifficultyMultiplier = difficultyMultiplier;
        RewardGold = rewardGold;
    }
}

/// <summary>Server -&gt; the one losing requester of a same-issue double-accept race for a Landlord Training
/// for Retainers issue.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLandlordTrainingIssueAcceptRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkLandlordTrainingIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
