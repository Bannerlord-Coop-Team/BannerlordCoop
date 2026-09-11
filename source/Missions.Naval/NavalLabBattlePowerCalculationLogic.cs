#if DEBUG
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

internal sealed class NavalLabBattlePowerCalculationLogic : MissionLogic, IBattlePowerCalculationLogic
{
    private readonly NavalLabBehavior fixture;

    public NavalLabBattlePowerCalculationLogic(NavalLabBehavior fixture)
    {
        this.fixture = fixture;
    }

    public float GetTotalTeamPower(Team team)
    {
        // This fixed, invulnerable fixture has no reserve troops or campaign spawn logic.
        float power = 0f;
        foreach (var agent in fixture.Agents)
        {
            if (agent.Team == team) power += agent.Character.GetPower();
        }
        return power;
    }
}
#endif
