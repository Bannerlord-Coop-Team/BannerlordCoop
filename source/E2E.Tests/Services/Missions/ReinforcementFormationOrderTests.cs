using E2E.Tests.Environment.MockEngine;
using Missions.Battles;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// #3462: a late allied party fields into a formation the host already commands. The batch must leave that
/// formation under the player's command instead of turning Delegate Command on and overriding the order.
/// Delegating paths set a Charge, which needs the live engine (see <see cref="MissionEngineFixture"/>), so
/// they are covered by the classifier tests in Coop.Tests.
/// </summary>
public class ReinforcementFormationOrderTests
{
    [Fact]
    public void LateAllyBatch_KeepsLocalPlayerFormationUnderPlayerCommand()
    {
        using var fixture = new MissionEngineFixture();
        var team = new MockTeam(BattleSideEnum.Attacker);
        var formation = team.GetFormation(FormationClass.Infantry);

        var action = ReinforcementFielder.ApplyJoinedFormation(
            formation.Shell, onLocalPlayerTeam: true, localCommanderActive: true, wasEmptyBeforeBatch: false);

        Assert.Equal(ReinforcementFielder.JoinedFormationAction.KeepPlayerOrder, action);
        Assert.False(formation.IsAIControlled);
        Assert.False(formation.MovementOrderSet);
    }

    // Count right after the first add: 1 is only the new troop, 2 means the formation already had a unit.
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void FirstAdd_RecordsWhetherFormationWasEmptyBeforeBatch(int countAfterFirstAdd, bool expectedEmpty)
    {
        using var fixture = new MissionEngineFixture();
        var formation = new MockTeam(BattleSideEnum.Attacker).GetFormation(FormationClass.Infantry).Shell;
        var joinedFormations = new Dictionary<Formation, bool>();

        ReinforcementFielder.RecordJoinedFormation(joinedFormations, formation, countAfterFirstAdd);

        Assert.Equal(expectedEmpty, joinedFormations[formation]);
    }

    [Fact]
    public void LaterAddsInBatch_KeepFirstAddsAnswer()
    {
        using var fixture = new MissionEngineFixture();
        var formation = new MockTeam(BattleSideEnum.Attacker).GetFormation(FormationClass.Infantry).Shell;
        var joinedFormations = new Dictionary<Formation, bool>();

        ReinforcementFielder.RecordJoinedFormation(joinedFormations, formation, countAfterAdd: 1);
        ReinforcementFielder.RecordJoinedFormation(joinedFormations, formation, countAfterAdd: 2);

        Assert.True(Assert.Single(joinedFormations).Value);
    }
}
