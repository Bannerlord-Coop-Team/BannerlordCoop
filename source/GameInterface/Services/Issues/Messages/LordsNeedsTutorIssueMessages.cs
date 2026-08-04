using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

/// <summary>
/// Which of the two never-reset, non-<c>[SaveableField]</c> booleans on
/// <see cref="LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest"/> is being mirrored - see
/// <see cref="Patches.LordsNeedsTutorQuestFlagCapturePatch"/> for where each is captured and
/// <see cref="Interfaces.LordsNeedsTutorQuestFlags"/> for the shadow-table this converges every peer onto.
/// </summary>
public enum LordsNeedsTutorQuestFlag : byte
{
    /// <summary>Mirrors <c>_doNotForceYoungHeroOutFromClan</c>, set true only inside the real
    /// <c>OnHeroesMarried</c> when the recorded owner's own <c>Hero.MainHero</c> married the young hero.</summary>
    DoNotForceYoungHeroOutFromClan = 0,
    /// <summary>Mirrors <c>_showQuestFailedConversation</c>, set true only inside the real <c>OnTimedOut</c>.</summary>
    ShowQuestFailedConversation = 1,
}

// --- Local events ---

/// <summary>
/// Published on the server (from <see cref="Patches.LordsNeedsTutorIssueCreationPatch"/>) immediately after a
/// genuine <c>IssueManager.CreateNewIssue</c> creates a new
/// <see cref="LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue"/>. Carries the live issue so
/// <see cref="Handlers.LordsNeedsTutorIssueHandler"/> can capture its one ctor-arg field (<c>_youngHero</c>) and
/// replicate it - see <see cref="Interfaces.ILordsNeedsTutorIssueInterface"/>'s type doc comment for why this
/// can't be safely re-derived independently on a client (the young-hero pick is a real, order-dependent
/// <c>FirstOrDefault</c> roll against <c>Clan.AliveLords</c>, not a pure function of the owner alone).
/// </summary>
public readonly struct LordsNeedsTutorIssueCreated : IEvent
{
    public readonly LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue Issue;

    public LordsNeedsTutorIssueCreated(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>
/// Published (on whichever machine's own genuine <c>OnHeroesMarried</c>/<c>OnTimedOut</c> invocation set the
/// corresponding flag true - always the recorded owner, see
/// <see cref="Patches.LordsNeedsTutorOwnershipGatePatches"/>) from
/// <see cref="Patches.LordsNeedsTutorQuestFlagCapturePatch"/>.
/// </summary>
public readonly struct LordsNeedsTutorQuestFlagTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly LordsNeedsTutorQuestFlag Flag;

    public LordsNeedsTutorQuestFlagTriggered(Hero owner, LordsNeedsTutorQuestFlag flag)
    {
        Owner = owner;
        Flag = flag;
    }
}

// --- Networked messages ---

/// <summary>Server -> all clients: the authoritative <c>_youngHero</c> for a newly created Lord Needs a Tutor
/// issue, so every client constructs a byte-identical instance instead of independently re-rolling
/// <c>ConditionsHold</c>'s own <c>FirstOrDefault</c> pick.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLordsNeedsTutorIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string YoungHeroId;

    public NetworkLordsNeedsTutorIssueCreated(string ownerId, string youngHeroId)
    {
        OwnerId = ownerId;
        YoungHeroId = youngHeroId;
    }
}

/// <summary>Client -> server: "my own genuine OnHeroesMarried/OnTimedOut just set this flag for hero X's
/// issue." A client's <c>SendAll</c> only reaches its one connection - the server - which re-broadcasts
/// <see cref="NetworkLordsNeedsTutorQuestFlagSet"/> to every peer.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestLordsNeedsTutorQuestFlagSet : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly LordsNeedsTutorQuestFlag Flag;

    public RequestLordsNeedsTutorQuestFlagSet(string ownerId, LordsNeedsTutorQuestFlag flag)
    {
        OwnerId = ownerId;
        Flag = flag;
    }
}

/// <summary>Server -> all clients: converge every peer's own shadow-table entry (and, if that peer already has
/// a live <see cref="LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest"/> for this hero, its own private
/// field too) onto this flag having been set. Idempotent - safe to apply repeatedly.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLordsNeedsTutorQuestFlagSet : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly LordsNeedsTutorQuestFlag Flag;

    public NetworkLordsNeedsTutorQuestFlagSet(string ownerId, LordsNeedsTutorQuestFlag flag)
    {
        OwnerId = ownerId;
        Flag = flag;
    }
}
