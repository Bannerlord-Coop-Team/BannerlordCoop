using Common.Util;
using GameInterface.Services.Party.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.Party;

public class GauntletPartyScreenPatchesTests
{
    /// <summary>
    /// The lord was talked to from the party screen and joined, so the server freed him and his
    /// companion from the live prison roster. The Done that vanilla runs on resume must not put
    /// either back.
    /// </summary>
    [Fact]
    public void ResumeAfterTalk_ServerReleasedPrisoners_StayOutAfterDoneReset()
    {
        var lord = new CharacterObject();
        var companion = new CharacterObject();
        var livePrisoners = TroopRoster.CreateDummyTroopRoster();
        var logic = CreateLogic(livePrisoners, livePrisoners, lord, companion);

        RemoveAll(livePrisoners, lord);
        RemoveAll(livePrisoners, companion);

        GauntletPartyScreenPatches.RebaseOnOwnerRosters(logic);
        logic.Reset(true);

        Assert.Equal(0, livePrisoners.Count);
        Assert.Equal(0, logic._initialData.RightPrisonerRoster.Count);
        Assert.False(logic.IsThereAnyChanges());
    }

    /// <summary>
    /// Same release, but the screen shows a copy of the party's prison roster, which the server
    /// change never reached.
    /// </summary>
    [Fact]
    public void ResumeAfterTalk_VisibleCopyOfPrisonRoster_IsRebuiltFromOwnerParty()
    {
        var lord = new CharacterObject();
        var companion = new CharacterObject();
        var ownerPrisoners = TroopRoster.CreateDummyTroopRoster();
        var visiblePrisoners = TroopRoster.CreateDummyTroopRoster();
        var logic = CreateLogic(ownerPrisoners, visiblePrisoners, lord, companion);
        visiblePrisoners.AddToCounts(lord, 1);
        visiblePrisoners.AddToCounts(companion, 1);
        logic._savedData = new PartyScreenData();
        logic._savedData.InitializeCopyFrom(null, null);
        logic._savedData.CopyFromScreenData(logic.CurrentData);

        RemoveAll(ownerPrisoners, companion);

        GauntletPartyScreenPatches.RebaseOnOwnerRosters(logic);
        logic.Reset(true);

        Assert.Equal(1, visiblePrisoners.GetTroopCount(lord));
        Assert.Equal(0, visiblePrisoners.GetTroopCount(companion));
        Assert.Equal(0, logic._initialData.RightPrisonerRoster.GetTroopCount(companion));
        Assert.Equal(0, logic._savedData.RightPrisonerRoster.GetTroopCount(companion));
    }

    private static PartyScreenLogic CreateLogic(
        TroopRoster ownerPrisoners,
        TroopRoster visiblePrisoners,
        params CharacterObject[] prisoners)
    {
        var members = TroopRoster.CreateDummyTroopRoster();
        var leftMembers = TroopRoster.CreateDummyTroopRoster();
        var leftPrisoners = TroopRoster.CreateDummyTroopRoster();
        var ownerParty = ObjectHelper.SkipConstructor<PartyBase>();
        ownerParty.MemberRoster = members;
        ownerParty.PrisonRoster = ownerPrisoners;

        var logic = new PartyScreenLogic();
        logic.RightOwnerParty = ownerParty;
        logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = members;
        logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = leftMembers;
        logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = visiblePrisoners;
        logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = leftPrisoners;
        logic.CurrentData.BindRostersFrom(members, visiblePrisoners, leftMembers, leftPrisoners, null, null);
        logic._initialData.InitializeCopyFrom(null, null);

        foreach (var prisoner in prisoners)
        {
            ownerPrisoners.AddToCounts(prisoner, 1);
            logic._initialData.RightPrisonerRoster.AddToCounts(prisoner, 1);
        }

        return logic;
    }

    private static void RemoveAll(TroopRoster roster, CharacterObject character)
    {
        roster.AddToCounts(character, -roster.GetTroopCount(character));
        roster.RemoveZeroCounts();
        roster.InitializeCachedData();
    }
}
