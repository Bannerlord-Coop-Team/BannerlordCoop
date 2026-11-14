using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Data;
using Helpers;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class NetworkCommitPartyScreenSerializationTest
{
    [Fact]
    public void RoundTrip_PreservesCommandsDoneKindAndUpgradeTarget()
    {
        var upgrade = new PartyScreenCommandData(
            PartyScreenLogic.PartyCommandCode.UpgradeTroop,
            PartyScreenLogic.PartyRosterSide.Right,
            PartyScreenLogic.TroopType.Member,
            characterId: 42,
            number: 3,
            woundedNumber: 1,
            upgradeTarget: 1);
        var original = new NetworkCommitPartyScreen(
            "hero-1",
            PartyScreenHelper.PartyScreenMode.TroopsManage,
            PartyScreenDoneKind.CreateClanParty,
            new[] { upgrade },
            new TroopRosterOrderData(new Dictionary<int, string> { [0] = "troop" }),
            "party-2",
            new TroopRosterData(new[] { new TroopRosterElementData(7, 2, 1, 50) }),
            default,
            isDonating: true,
            doNotApplyGoldTransactions: true,
            "force-3",
            "giver-4",
            "issue-5");

        var result = RoundTrip(original);

        Assert.Equal("hero-1", result.MainHeroId);
        Assert.Equal(PartyScreenHelper.PartyScreenMode.TroopsManage, result.Mode);
        Assert.Equal(PartyScreenDoneKind.CreateClanParty, result.DoneKind);
        var command = Assert.Single(result.Commands);
        Assert.Equal(
            (upgrade.Code, upgrade.Side, upgrade.Type, upgrade.CharacterId, upgrade.Number, upgrade.WoundedNumber, upgrade.UpgradeTarget),
            (command.Code, command.Side, command.Type, command.CharacterId, command.Number, command.WoundedNumber, command.UpgradeTarget));
        Assert.Equal("troop", result.RightMemberOrderData.IndexCharacterIds[0]);
        Assert.Equal("party-2", result.LeftPartyId);
        Assert.Equal(50, Assert.Single(result.LeftMemberSeed.Data).Xp);
        Assert.True(result.IsDonating);
        Assert.True(result.DoNotApplyGoldTransactions);
        Assert.Equal(("force-3", "giver-4", "issue-5"), (result.ForceTransferId, result.IssueOwnerId, result.IssueId));
    }

    // protobuf-net writes nothing for an empty array of structs, so a Done with no commands arrives with null.
    [Fact]
    public void RoundTrip_EmptyCommands_ArriveAsNull()
    {
        var original = new NetworkCommitPartyScreen(
            "hero-1",
            PartyScreenHelper.PartyScreenMode.Normal,
            PartyScreenDoneKind.Default,
            Array.Empty<PartyScreenCommandData>(),
            new TroopRosterOrderData(new Dictionary<int, string>()),
            null,
            default,
            default,
            isDonating: false,
            doNotApplyGoldTransactions: false,
            null,
            null,
            null);

        var result = RoundTrip(original);

        Assert.Null(result.Commands);
        Assert.Equal(PartyScreenDoneKind.Default, result.DoneKind);
    }

    private static NetworkCommitPartyScreen RoundTrip(NetworkCommitPartyScreen original)
    {
        using var stream = new MemoryStream();
        RuntimeTypeModel.Default.Serialize(stream, original);
        stream.Position = 0;
        return (NetworkCommitPartyScreen)RuntimeTypeModel.Default.Deserialize(stream, null, typeof(NetworkCommitPartyScreen));
    }
}
