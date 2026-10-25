using Common.Util;
using GameInterface.Services.Party.Patches;
using System;
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
        var live = CreateParty();
        var logic = CreateLogic(live, live.MemberRoster, live.PrisonRoster);
        AddEverywhere(logic, live.PrisonRoster, data => data.RightPrisonerRoster, lord, companion);

        RemoveAll(live.PrisonRoster, lord);
        RemoveAll(live.PrisonRoster, companion);

        GauntletPartyScreenPatches.RebaseOnOwnerRosters(logic);
        logic.Reset(true);

        Assert.Equal(0, live.PrisonRoster.Count);
        Assert.Equal(0, logic._initialData.RightPrisonerRoster.Count);
        Assert.Equal(0, logic._savedData.RightPrisonerRoster.Count);
        Assert.False(logic.IsThereAnyChanges());
    }

    /// <summary>
    /// The create clan party screen shows copies of the main party's rosters with the moved hero
    /// taken out. Only what the server removed may leave them, the hero must not come back.
    /// </summary>
    [Fact]
    public void ResumeAfterTalk_CopiedRosters_OnlyLoseWhatTheServerRemoved()
    {
        var hero = new CharacterObject();
        var regular = new CharacterObject();
        var prisoner = new CharacterObject();
        var freedPrisoner = new CharacterObject();
        var live = CreateParty();
        live.MemberRoster.AddToCounts(hero, 1);
        live.MemberRoster.AddToCounts(regular, 3);
        live.PrisonRoster.AddToCounts(prisoner, 1);
        live.PrisonRoster.AddToCounts(freedPrisoner, 1);

        var shownMembers = TroopRoster.CreateDummyTroopRoster();
        var shownPrisoners = TroopRoster.CreateDummyTroopRoster();
        var logic = CreateLogic(live, shownMembers, shownPrisoners);
        AddEverywhere(logic, shownMembers, data => data.RightMemberRoster, regular, regular, regular);
        AddEverywhere(logic, shownPrisoners, data => data.RightPrisonerRoster, prisoner, freedPrisoner);
        AddEverywhere(logic, logic.CurrentData.LeftMemberRoster, data => data.LeftMemberRoster, hero);

        RemoveAll(live.PrisonRoster, freedPrisoner);

        GauntletPartyScreenPatches.RebaseOnOwnerRosters(logic);
        logic.Reset(true);

        Assert.Equal(0, shownMembers.GetTroopCount(hero));
        Assert.Equal(3, shownMembers.GetTroopCount(regular));
        Assert.Equal(1, logic.CurrentData.LeftMemberRoster.GetTroopCount(hero));
        Assert.Equal(1, shownPrisoners.GetTroopCount(prisoner));
        Assert.Equal(0, shownPrisoners.GetTroopCount(freedPrisoner));
        Assert.Equal(0, logic._savedData.RightPrisonerRoster.GetTroopCount(freedPrisoner));
        Assert.Equal(1, live.MemberRoster.GetTroopCount(hero));
    }

    /// <summary>
    /// With a left party open (clan party or garrison), troops the server took out of its live
    /// roster during the talk stay out too once the Done on resume resets the screen.
    /// </summary>
    [Fact]
    public void ResumeAfterTalk_LeftPartyMemberRemovedByServer_StaysOut()
    {
        var companion = new CharacterObject();
        var regular = new CharacterObject();
        var prisoner = new CharacterObject();
        var right = CreateParty();
        var left = CreateParty();
        var logic = CreateLogic(right, right.MemberRoster, right.PrisonRoster, left);
        AddEverywhere(logic, left.MemberRoster, data => data.LeftMemberRoster, companion, regular);
        AddEverywhere(logic, right.PrisonRoster, data => data.RightPrisonerRoster, prisoner);

        // Vanilla only resets when the right side differs, so the server frees a prisoner as well.
        RemoveAll(left.MemberRoster, companion);
        RemoveAll(right.PrisonRoster, prisoner);

        GauntletPartyScreenPatches.RebaseOnOwnerRosters(logic);
        logic.Reset(true);

        Assert.Equal(0, left.MemberRoster.GetTroopCount(companion));
        Assert.Equal(1, left.MemberRoster.GetTroopCount(regular));
    }

    private static PartyBase CreateParty()
    {
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        party.MemberRoster = TroopRoster.CreateDummyTroopRoster();
        party.PrisonRoster = TroopRoster.CreateDummyTroopRoster();
        return party;
    }

    private static PartyScreenLogic CreateLogic(
        PartyBase rightOwner,
        TroopRoster rightMembers,
        TroopRoster rightPrisoners,
        PartyBase leftOwner = null)
    {
        var leftMembers = leftOwner?.MemberRoster ?? TroopRoster.CreateDummyTroopRoster();
        var leftPrisoners = leftOwner?.PrisonRoster ?? TroopRoster.CreateDummyTroopRoster();

        var logic = new PartyScreenLogic();
        logic.RightOwnerParty = rightOwner;
        logic.LeftOwnerParty = leftOwner;
        logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = rightMembers;
        logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = leftMembers;
        logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right] = rightPrisoners;
        logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = leftPrisoners;
        logic.CurrentData.BindRostersFrom(rightMembers, rightPrisoners, leftMembers, leftPrisoners, null, null);
        logic._initialData.InitializeCopyFrom(null, null);
        logic._savedData = new PartyScreenData();
        logic._savedData.InitializeCopyFrom(null, null);
        return logic;
    }

    private static void AddEverywhere(
        PartyScreenLogic logic,
        TroopRoster shown,
        Func<PartyScreenData, TroopRoster> slot,
        params CharacterObject[] troops)
    {
        foreach (var troop in troops)
        {
            shown.AddToCounts(troop, 1);
            slot(logic._initialData).AddToCounts(troop, 1);
            slot(logic._savedData).AddToCounts(troop, 1);
        }
    }

    private static void RemoveAll(TroopRoster roster, CharacterObject character)
    {
        roster.AddToCounts(character, -roster.GetTroopCount(character));
        roster.RemoveZeroCounts();
        roster.InitializeCachedData();
    }
}
