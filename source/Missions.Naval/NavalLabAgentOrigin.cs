#if DEBUG
using System;
using TaleWorlds.Core;

namespace Missions.Naval;

// Synthetic origins never forward mission outcomes to a campaign allocation.
internal sealed class NavalLabAgentOrigin : IAgentOriginBase
{
    private readonly Action<string> reject;
    public NavalLabAgentOrigin(BasicCharacterObject troop, int seed, bool owned, Action<string> reject)
    {
        Troop = troop;
        UniqueSeed = seed;
        IsUnderPlayersCommand = owned;
        this.reject = reject;
    }
    public bool IsUnderPlayersCommand { get; }
    public bool IsInSameArmyAsPlayer => false;
    public uint FactionColor => 0xff224488;
    public uint FactionColor2 => 0xffdddddd;
    public IBattleCombatant BattleCombatant => null;
    public int UniqueSeed { get; }
    public int Seed => UniqueSeed;
    public Banner Banner { get; private set; }
    public BasicCharacterObject Troop { get; }
    public bool HasThrownWeapon => false;
    public bool HasHeavyArmor => false;
    public bool HasShield => false;
    public bool HasSpear => false;
    public void SetWounded() => reject("origin.wounded");
    public void SetKilled() => reject("origin.killed");
    public void SetRouted(bool isOrderRetreat) => reject("origin.routed");
    public void OnAgentRemoved(float agentHealth) { }
    public void OnScoreHit(BasicCharacterObject victim, BasicCharacterObject captain, int damage,
        bool isFatal, bool isTeamKill, WeaponComponentData weapon) => reject("origin.scoreHit");
    public void SetBanner(Banner banner) => Banner = banner;
    public TroopTraitsMask GetTraitsMask() => default;
}
#endif
