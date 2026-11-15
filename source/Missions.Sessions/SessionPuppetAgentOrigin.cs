using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Sessions;

/// <summary>
/// Origin of a session puppet: the team's colours and banner. The spawn takes the banner from the origin, so the
/// puppet shows the same banner as the owner's script spawn (Mission.GetAgentBuildDataToSpawnTroop).
/// </summary>
internal sealed class SessionPuppetAgentOrigin : IAgentOriginBase
{
    private readonly BasicCharacterObject troop;
    private readonly Team team;
    private readonly UniqueTroopDescriptor descriptor;
    private readonly bool hasThrownWeapon, hasSpear, hasShield, hasHeavyArmor;
    private Banner banner;

    public SessionPuppetAgentOrigin(BasicCharacterObject troop, Team team, UniqueTroopDescriptor descriptor)
    {
        this.troop = troop;
        this.team = team;
        this.descriptor = descriptor;
        banner = team.Banner;
        AgentOriginUtilities.GetDefaultTroopTraits(troop, out hasThrownWeapon, out hasSpear, out hasShield, out hasHeavyArmor);
    }

    public bool IsUnderPlayersCommand => false;
    public bool IsInSameArmyAsPlayer => false;
    public uint FactionColor => team.Color;
    public uint FactionColor2 => team.Color2;
    public IBattleCombatant BattleCombatant => null;
    public int UniqueSeed => descriptor.UniqueSeed;
    public int Seed => 0;
    public Banner Banner => banner;
    public BasicCharacterObject Troop => troop;

    bool IAgentOriginBase.HasThrownWeapon => hasThrownWeapon;
    bool IAgentOriginBase.HasHeavyArmor => hasHeavyArmor;
    bool IAgentOriginBase.HasShield => hasShield;
    bool IAgentOriginBase.HasSpear => hasSpear;

    // A puppet's removal is counted on its owner.
    public void SetWounded() { }
    public void SetKilled() { }
    public void SetRouted(bool isOrderRetreat) { }
    public void OnAgentRemoved(float agentHealth) { }
    public void OnScoreHit(BasicCharacterObject victim, BasicCharacterObject formationCaptain, int damage, bool isFatal, bool isTeamKill, WeaponComponentData attackerWeapon) { }
    public void SetBanner(Banner value) => banner = value;
    public TroopTraitsMask GetTraitsMask() => AgentOriginUtilities.GetDefaultTraitsMask(this);
}
