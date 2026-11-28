using Common.Messaging;
using GameInterface.Services.Party.Data;
using GameInterface.Services.TroopRosters.Data;
using Helpers;
using ProtoBuf;

namespace GameInterface.Services.Party.Messages;

/// <summary>
/// Everything a party screen Done needs on the server: the commands the player ran, in order, and what the
/// left side of the screen was, so the server can replay them against its own rosters.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCommitPartyScreen : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly PartyScreenHelper.PartyScreenMode Mode;

    [ProtoMember(3)]
    public readonly PartyScreenDoneKind DoneKind;

    [ProtoMember(4)]
    public readonly PartyScreenCommandData[] Commands;

    [ProtoMember(5)]
    public readonly TroopRosterOrderData RightMemberOrderData;

    // Set when the left side is a real party, whose rosters the server edits directly.
    [ProtoMember(6)]
    public readonly string LeftPartyId;

    // What a client-only left side started with, like a loot or quest roster.
    [ProtoMember(7)]
    public readonly TroopRosterData LeftMemberSeed;

    [ProtoMember(8)]
    public readonly TroopRosterData LeftPrisonerSeed;

    [ProtoMember(9)]
    public readonly bool IsDonating;

    [ProtoMember(10)]
    public readonly bool DoNotApplyGoldTransactions;

    [ProtoMember(11)]
    public readonly string ForceTransferId;

    [ProtoMember(12)]
    public readonly string IssueOwnerId;

    [ProtoMember(13)]
    public readonly string IssueId;

    public NetworkCommitPartyScreen(
        string mainHeroId,
        PartyScreenHelper.PartyScreenMode mode,
        PartyScreenDoneKind doneKind,
        PartyScreenCommandData[] commands,
        TroopRosterOrderData rightMemberOrderData,
        string leftPartyId,
        TroopRosterData leftMemberSeed,
        TroopRosterData leftPrisonerSeed,
        bool isDonating,
        bool doNotApplyGoldTransactions,
        string forceTransferId,
        string issueOwnerId,
        string issueId)
    {
        MainHeroId = mainHeroId;
        Mode = mode;
        DoneKind = doneKind;
        Commands = commands;
        RightMemberOrderData = rightMemberOrderData;
        LeftPartyId = leftPartyId;
        LeftMemberSeed = leftMemberSeed;
        LeftPrisonerSeed = leftPrisonerSeed;
        IsDonating = isDonating;
        DoNotApplyGoldTransactions = doNotApplyGoldTransactions;
        ForceTransferId = forceTransferId;
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
    }
}
