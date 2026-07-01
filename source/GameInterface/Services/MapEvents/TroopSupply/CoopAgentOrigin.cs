using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// Agent origin for server-supplied coop battle troops. Mirrors <c>SimpleAgentOrigin</c> but carries the
/// troop's <see cref="Party"/> for EVERY troop (not just heroes) — <c>SimpleAgentOrigin.Party</c> is null for
/// non-heroes, which leaves <c>BattleCombatant</c> null so the engine can't put an enemy soldier on a team
/// (it never spawns) and can't recognise the player's hero (the player isn't attached). Casualty hooks are
/// no-ops: deaths flow through the existing <c>Agent.Die</c> → server path, so applying them here too would
/// double-count.
/// </summary>
public class CoopAgentOrigin : IAgentOriginBase
{
    private readonly CharacterObject _troop;
    private readonly PartyBase _party;
    private readonly UniqueTroopDescriptor _descriptor;
    private Banner _banner;
    private readonly bool _hasThrownWeapon, _hasSpear, _hasShield, _hasHeavyArmor;

    public BasicCharacterObject Troop => _troop;
    public PartyBase Party => _party;
    public IBattleCombatant BattleCombatant => _party;
    public int Rank { get; }
    public int UniqueSeed => _descriptor.UniqueSeed;

    bool IAgentOriginBase.HasThrownWeapon => _hasThrownWeapon;
    bool IAgentOriginBase.HasHeavyArmor => _hasHeavyArmor;
    bool IAgentOriginBase.HasShield => _hasShield;
    bool IAgentOriginBase.HasSpear => _hasSpear;

    public CoopAgentOrigin(CharacterObject troop, PartyBase party, int rank, Banner banner, UniqueTroopDescriptor descriptor)
    {
        _troop = troop;
        _party = party;
        _descriptor = descriptor;
        Rank = rank == -1 ? MBRandom.RandomInt(10000) : rank;
        _banner = banner;
        AgentOriginUtilities.GetDefaultTroopTraits(_troop, out _hasThrownWeapon, out _hasSpear, out _hasShield, out _hasHeavyArmor);
    }

    public bool IsUnderPlayersCommand
    {
        get
        {
            if (_party == null) return false;
            if (_party != PartyBase.MainParty && _party.Owner != Hero.MainHero)
                return _party.MapFaction?.Leader == Hero.MainHero;
            return true;
        }
    }

    public bool IsInSameArmyAsPlayer
    {
        get
        {
            MobileParty mobileParty;
            Army army;
            if (_party != null && (mobileParty = _party.MobileParty) != null && (army = mobileParty.Army) != null
                && army == MobileParty.MainParty?.Army && (army.LeaderParty == mobileParty || mobileParty.AttachedTo == army.LeaderParty))
            {
                if (army.LeaderParty != MobileParty.MainParty)
                    return MobileParty.MainParty.AttachedTo == army.LeaderParty;
                return true;
            }
            return false;
        }
    }

    public uint FactionColor => _party?.MapFaction != null
        ? _party.MapFaction.Color
        : (_troop.IsHero ? _troop.HeroObject.MapFaction.Color : 0u);

    public uint FactionColor2 => _party?.MapFaction != null
        ? _party.MapFaction.Color2
        : (_troop.IsHero ? _troop.HeroObject.MapFaction.Color2 : 0u);

    public int Seed => _party != null
        ? CharacterHelper.GetPartyMemberFaceSeed(_party, _troop, Rank)
        : CharacterHelper.GetDefaultFaceSeed(_troop, Rank);

    public Banner Banner
    {
        get
        {
            if (_banner != null) return _banner;
            if (_party?.MapFaction != null) return _party.MapFaction.Banner;
            return _party?.LeaderHero?.ClanBanner;
        }
    }

    public void SetWounded() { }
    public void SetKilled() { }
    public void SetRouted(bool isOrderRetreat) { }
    public void OnAgentRemoved(float agentHealth) { }
    void IAgentOriginBase.OnScoreHit(BasicCharacterObject victim, BasicCharacterObject formationCaptain, int damage, bool isFatal, bool isTeamKill, WeaponComponentData attackerWeapon) { }

    public void SetBanner(Banner banner) => _banner = banner;

    TroopTraitsMask IAgentOriginBase.GetTraitsMask() => AgentOriginUtilities.GetDefaultTraitsMask(this);
}
