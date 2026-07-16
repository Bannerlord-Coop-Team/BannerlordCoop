using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEventParties.Messages;
using Helpers;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.TroopSupply;

/// <summary>
/// Agent origin for server-supplied coop battle troops. It carries each troop's party so the engine can assign
/// teams, reports removals to the supplier that minted it so the engine's reinforcement quota advances (while
/// leaving roster casualties to the network death path), and preserves the controlled hero's final health.
/// Score hits are reported here because they have no other server path.
/// </summary>
public class CoopAgentOrigin : IAgentOriginBase
{
    private readonly CharacterObject _troop;
    private readonly PartyBase _party;
    private readonly UniqueTroopDescriptor _descriptor;
    // The CoopTroopSupplier this origin was supplied from, or null for origins built outside a supplier
    // (puppets — PuppetSpawner / ReinforcementFielder): those are counted on their owner's client, never here.
    private readonly CoopTroopSupplier _supplier;
    // One-shot removal latch (native PartyGroupAgentOrigin's _isRemoved): 0 = standing, 1 = already counted.
    private int _removedLatch;
    private Banner _banner;
    private readonly bool _hasThrownWeapon, _hasSpear, _hasShield, _hasHeavyArmor;

    public BasicCharacterObject Troop => _troop;
    public PartyBase Party => _party;
    public IBattleCombatant BattleCombatant => _party;
    public int Rank { get; }
    public int UniqueSeed => _descriptor.UniqueSeed;

    /// <summary>The server's MapEventParty id this troop was supplied under, carried so the spawn
    /// broadcast doesn't depend on re-deriving it from the local map-event membership.</summary>
    public string MapEventPartyId { get; }

    bool IAgentOriginBase.HasThrownWeapon => _hasThrownWeapon;
    bool IAgentOriginBase.HasHeavyArmor => _hasHeavyArmor;
    bool IAgentOriginBase.HasShield => _hasShield;
    bool IAgentOriginBase.HasSpear => _hasSpear;

    public CoopAgentOrigin(CharacterObject troop, PartyBase party, int rank, Banner banner, UniqueTroopDescriptor descriptor, string mapEventPartyId = null, CoopTroopSupplier supplier = null)
    {
        _troop = troop;
        _party = party;
        _descriptor = descriptor;
        _supplier = supplier;
        Rank = rank == -1 ? MBRandom.RandomInt(10000) : rank;
        _banner = banner;
        MapEventPartyId = mapEventPartyId;
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

    // [BR-073] Removal reports feed the supplier's NumRemovedTroops — the engine's ONLY casualty input for
    // reinforcements (NumberOfActiveTroops = _numSpawnedTroops - supplier.NumRemovedTroops, and
    // ComputeWaveBatch requests a wave only once enough of the initial allotment is removed). Without this
    // feedback the engine believes every initial troop stands forever and never pulls a wave (the live
    // 600-of-2000 stall). ENGINE QUOTA ONLY: roster casualties still flow exclusively through the network
    // death path (MapEventParty.OnTroop* via the owner→server messages) — nothing here touches rosters.
    public void SetWounded() { if (TryLatchRemoval()) _supplier.OnTroopWounded(_descriptor); }
    public void SetKilled() { if (TryLatchRemoval()) _supplier.OnTroopKilled(_descriptor); }
    public void SetRouted(bool isOrderRetreat) { if (TryLatchRemoval()) _supplier.OnTroopRouted(_descriptor, isOrderRetreat); }

    // An agent can be reported removed more than once (a Wounded knockdown then a Killed finish, or a
    // duplicate replicated removal), but must count exactly once against the quota — hence the one-shot
    // latch, interlocked because replicated removals can arrive off the game thread. Origins without a
    // supplier (puppets) never latch: their removals are not this client's to count.
    private bool TryLatchRemoval() => _supplier != null && Interlocked.Exchange(ref _removedLatch, 1) == 0;

    public void OnAgentRemoved(float agentHealth)
    {
        // Unlike the casualty hooks above, final agent health has no other path back to Hero.HitPoints. Vanilla
        // performs this transfer in the origin hook, which also covers injured survivors removed during teardown.
        if (!_troop.IsHero) return;

        var hero = _troop.HeroObject;
        if (hero.HeroState == Hero.CharacterStates.Dead) return;
        if (!hero.IsControlledByThisInstance()) return;

        hero.HitPoints = MathF.Max(1, MathF.Round(agentHealth));
    }

    // Unlike the casualty hooks above, score hits have NO other path to the map event party in a coop battle
    // (the native PartyGroupAgentOrigin → supplier chain is substituted away), so this is where they are
    // reported. Fires exactly once per blow across the mesh — a blow is applied only on the victim owner's
    // client (BattleBlowInterceptPatch routes the rest), and that client's BattleAgentLogic.OnAgentHit calls
    // the ATTACKER's origin, ours for troops and puppets alike. The server accounts the hit against the
    // authoritative roster (the descriptor seed is server-minted, so it resolves there) and the resulting
    // ContributionToBattle comes back through autosync.
    void IAgentOriginBase.OnScoreHit(BasicCharacterObject victim, BasicCharacterObject formationCaptain, int damage, bool isFatal, bool isTeamKill, WeaponComponentData attackerWeapon)
    {
        if (isTeamKill || damage <= 0) return;
        if (victim is not CharacterObject attackedTroop) return;

        var mapEventParty = FindMapEventParty();
        if (mapEventParty == null) return;

        MessageBroker.Instance.Publish(this, new OnTroopScoreHitAttempted(
            mapEventParty, _descriptor.UniqueSeed, attackedTroop, damage, isFatal, isSimulatedHit: false));
    }

    // The map event party this troop fights under, resolved lazily: the origin outlives battle setup, and
    // during teardown the party's side is already null — then the hit is simply not reported.
    private MapEventParty FindMapEventParty()
    {
        var side = _party?.MapEventSide;
        if (side == null) return null;

        foreach (var mapEventParty in side.Parties)
            if (mapEventParty.Party == _party) return mapEventParty;

        return null;
    }

    public void SetBanner(Banner banner) => _banner = banner;

    TroopTraitsMask IAgentOriginBase.GetTraitsMask() => AgentOriginUtilities.GetDefaultTraitsMask(this);
}
