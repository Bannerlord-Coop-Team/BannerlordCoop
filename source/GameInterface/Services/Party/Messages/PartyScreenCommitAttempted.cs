using Common.Messaging;
using GameInterface.Services.Party.Data;
using Helpers;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party.Messages;

/// <summary>
/// The player pressed Done on a party screen and vanilla's done handler accepted it. Carries what the commit
/// needs from the screen, taken before the screen resets.
/// </summary>
internal readonly struct PartyScreenCommitAttempted : IEvent
{
    public readonly Hero MainHero;
    public readonly PartyScreenHelper.PartyScreenMode Mode;
    public readonly PartyScreenDoneKind DoneKind;
    public readonly bool IsDonating;
    public readonly bool DoNotApplyGoldTransactions;
    public readonly string ForceTransferId;
    public readonly IReadOnlyList<PartyScreenLogic.PartyCommand> Commands;
    public readonly PartyBase LeftOwnerParty;
    // The screen's own left roster, which a quest picker is keyed by.
    public readonly TroopRoster LeftMemberRoster;
    // Copies of what the left side started with and of the right members, for their order.
    public readonly TroopRoster InitialLeftMembers;
    public readonly TroopRoster InitialLeftPrisoners;
    public readonly TroopRoster RightMembers;

    public PartyScreenCommitAttempted(
        Hero mainHero,
        PartyScreenHelper.PartyScreenMode mode,
        PartyScreenDoneKind doneKind,
        bool isDonating,
        bool doNotApplyGoldTransactions,
        string forceTransferId,
        IReadOnlyList<PartyScreenLogic.PartyCommand> commands,
        PartyBase leftOwnerParty,
        TroopRoster leftMemberRoster,
        TroopRoster initialLeftMembers,
        TroopRoster initialLeftPrisoners,
        TroopRoster rightMembers)
    {
        MainHero = mainHero;
        Mode = mode;
        DoneKind = doneKind;
        IsDonating = isDonating;
        DoNotApplyGoldTransactions = doNotApplyGoldTransactions;
        ForceTransferId = forceTransferId;
        Commands = commands;
        LeftOwnerParty = leftOwnerParty;
        LeftMemberRoster = leftMemberRoster;
        InitialLeftMembers = initialLeftMembers;
        InitialLeftPrisoners = initialLeftPrisoners;
        RightMembers = rightMembers;
    }
}
