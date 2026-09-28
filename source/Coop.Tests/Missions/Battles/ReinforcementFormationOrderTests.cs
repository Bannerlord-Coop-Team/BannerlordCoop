using Missions.Battles;
using Xunit;
using JoinedFormationAction = Missions.Battles.ReinforcementFielder.JoinedFormationAction;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// #3462: late and recovered troops land in the host's own formations, so a fielding batch must only hand a
/// formation to the AI when the local player is not commanding it.
/// </summary>
public class ReinforcementFormationOrderTests
{
    [Theory]
    // Enemy or ally team: nobody local commands it.
    [InlineData(false, true, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(false, true, true, true)]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, true, true)]
    // Host hero dead, knocked out or not spawned.
    [InlineData(true, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, true, true)]
    // Already AI-controlled: delegated by the player, emptied mid-battle, or a sergeant's other formations.
    [InlineData(true, true, true, false)]
    [InlineData(true, true, true, true)]
    public void FormationNotCommandedByLocalPlayer_DelegatesAndCharges(
        bool onLocalPlayerTeam, bool localCommanderActive, bool isAIControlled, bool wasEmptyBeforeBatch)
    {
        var action = ReinforcementFielder.ClassifyJoinedFormation(
            onLocalPlayerTeam, localCommanderActive, isAIControlled, wasEmptyBeforeBatch);

        Assert.Equal(JoinedFormationAction.DelegateAndCharge, action);
    }

    [Fact]
    public void PlayerCommandedFormationWithUnits_KeepsPlayerOrder()
    {
        var action = ReinforcementFielder.ClassifyJoinedFormation(
            onLocalPlayerTeam: true, localCommanderActive: true, isAIControlled: false, wasEmptyBeforeBatch: false);

        Assert.Equal(JoinedFormationAction.KeepPlayerOrder, action);
    }

    [Fact]
    public void PlayerCommandedEmptyFormation_ChargesWithoutDelegating()
    {
        var action = ReinforcementFielder.ClassifyJoinedFormation(
            onLocalPlayerTeam: true, localCommanderActive: true, isAIControlled: false, wasEmptyBeforeBatch: true);

        Assert.Equal(JoinedFormationAction.ChargeWithoutDelegating, action);
    }
}
