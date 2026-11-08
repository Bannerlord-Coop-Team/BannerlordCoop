#if DEBUG
using Common;
using Common.Commands;
using Common.Messaging;
using GameInterface;
using GameInterface.Services.MapEvents.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Missions.Diagnostics;

public interface IHeroCombatFixture : IHandler
{
    CoopCommandResult Prepare();
    CoopCommandResult Hit(bool ranged, int count);
    CoopCommandResult Restore();
    string Snapshot();
}

/// <summary>
/// DEBUG cheat for #4499: a hero agent can't be made to fight from a script (it is player controlled, and the AI of a
/// lone general formation doesn't engage), so this injects hero hits through the hit path a native hit takes, and
/// tallies each hero hit reward next to the skill xp vanilla gives for it, so the server's xp can be compared.
/// Melee blows go through <c>Agent.RegisterBlow</c>, which the co-op damage router sends to the victim's owner.
/// A ranged blow resolves its weapon from a live missile that only exists on the shooter's client, so it calls
/// <c>Mission.OnAgentHit</c> directly and does not damage the victim.
/// </summary>
internal sealed class HeroCombatFixture : IHeroCombatFixture
{
    private sealed class Tally
    {
        public string Hero;
        public string Skill;
        public string Weapon;
        public int Hits;
        public int HitsWithoutWeapon;
        public double Damage;
        public double ExpectedSkillXp;
        public double RawXp;
    }

    private readonly IMessageBroker messageBroker;
    private readonly object gate = new object();
    private readonly Dictionary<string, Tally> tallies = new Dictionary<string, Tally>();
    private readonly Dictionary<Agent, Agent.MortalityState> previousMortality = new Dictionary<Agent, Agent.MortalityState>();
    private int nonHeroHits;
    private int nonHeroHitsWithCommander;
    private double nonHeroDamage;

    public HeroCombatFixture(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
        messageBroker.Subscribe<BattleHitReward>(Handle_BattleHitReward);
    }

    public void Dispose() => messageBroker.Unsubscribe<BattleHitReward>(Handle_BattleHitReward);

    private void Handle_BattleHitReward(MessagePayload<BattleHitReward> payload)
    {
        var hit = payload.What;
        if (hit.AffectorCharacter == null) return;

        lock (gate)
        {
            if (!hit.AffectorCharacter.IsHero)
            {
                nonHeroHits++;
                nonHeroDamage += hit.DamageAmount;
                // The commander's tactics xp is the other reward a non-hero hit gives on the server.
                if (hit.Hero != null && hit.Hero.PartyBelongedTo != null) nonHeroHitsWithCommander++;
                return;
            }

            // Same arithmetic as DefaultSkillLevelingManager.OnCombatHit for a hero attacker, then Hero.AddSkillXp.
            Hero hero = hit.AffectorCharacter.HeroObject;
            if (hero?.HeroDeveloper == null || hit.AffectedAgentSide == hit.AffectorAgentSide) return;

            var models = Campaign.Current.Models;
            var xp = new ExplainedNumber(models.CombatXpModel.GetXpFromHit(
                hero.CharacterObject,
                hit.Captain?.CharacterObject,
                hit.AffectedCharacter,
                hero.PartyBelongedTo?.Party,
                (int)hit.DamageAmount,
                hit.AffectedAgentHealth < 1f,
                CombatXpModel.MissionTypeEnum.Battle).ResultNumber);

            SkillObject skill;
            if (hit.LastAttackerWeapon != null)
            {
                skill = models.CombatXpModel.GetSkillForWeapon(hit.LastAttackerWeapon, hit.IsSiegeEngineHit);
                if (hit.LastShotDifficulty > 0f)
                {
                    float bowFactor = skill == DefaultSkills.Bow ? 0.5f : 1f;
                    xp.AddFactor(bowFactor * models.CombatXpModel.GetXpMultiplierFromShotDifficulty(hit.LastShotDifficulty));
                }
            }
            else
            {
                bool isHorseCharge = hit.IsAgentMounted && hit.AttackType == AgentAttackType.Collision;
                skill = isHorseCharge ? DefaultSkills.Riding : DefaultSkills.Athletics;
            }

            double raw = xp.RoundedResultNumber;
            double expected = raw * models.GenericXpModel.GetXpMultiplier(hero) * hero.HeroDeveloper.GetFocusFactor(skill);
            string weaponClass = hit.LastAttackerWeapon?.WeaponClass.ToString() ?? "none";
            string key = hero.StringId + "|" + skill.StringId + "|" + weaponClass;
            if (!tallies.TryGetValue(key, out Tally tally))
            {
                tally = new Tally { Hero = hero.StringId, Skill = skill.StringId, Weapon = weaponClass };
                tallies[key] = tally;
            }

            tally.Hits++;
            if (hit.LastAttackerWeapon == null) tally.HitsWithoutWeapon++;
            tally.Damage += hit.DamageAmount;
            tally.ExpectedSkillXp += expected;
            tally.RawXp += raw;
        }
    }

    private Agent[] LocalHeroes(Mission mission)
    {
        if (mission == null || !ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry)) return Array.Empty<Agent>();

        return mission.Agents
            .Where(agent => agent.IsHuman && agent.IsActive() && (agent.Character as CharacterObject)?.IsHero == true
                && registry.IsLocallyControlled(agent))
            .ToArray();
    }

    /// <summary>Gives the heroes this client controls a bow if they have none, and makes them immortal.</summary>
    public CoopCommandResult Prepare()
    {
        Mission mission = Mission.Current;
        Agent[] heroes = LocalHeroes(mission);
        if (heroes.Length == 0)
            return Failed("This client controls no active hero agent");

        var notes = new List<string>();
        lock (gate)
        {
            foreach (Agent agent in heroes)
            {
                notes.Add(EquipBow(agent));
                if (!previousMortality.ContainsKey(agent)) previousMortality[agent] = agent.CurrentMortalityState;
                agent.SetMortalityState(Agent.MortalityState.Immortal);
            }
        }

        return new CoopCommandResult(true, $"HERO_COMBAT_PREPARED heroes={heroes.Length} {string.Join(" ", notes)}\nLIVE_TEST_JSON=" + Snapshot());
    }

    /// <summary>Injects <paramref name="count"/> hits per hero on its nearest enemy.</summary>
    public CoopCommandResult Hit(bool ranged, int count)
    {
        Mission mission = Mission.Current;
        Agent[] heroes = LocalHeroes(mission);
        if (heroes.Length == 0)
            return Failed("This client controls no active hero agent");

        int injected = 0;
        foreach (Agent hero in heroes)
        {
            for (int i = 0; i < count; i++)
            {
                Agent victim = NearestEnemy(mission, hero, out _);
                if (victim == null) break;

                if (ranged ? InjectRangedHit(mission, hero, victim) : InjectMeleeHit(hero, victim)) injected++;
            }
        }

        return new CoopCommandResult(true, $"HERO_COMBAT_HIT ranged={ranged} injected={injected} heroes={heroes.Length}");
    }

    public CoopCommandResult Restore()
    {
        lock (gate)
        {
            // Agents of an earlier mission are gone with it; only touch the current mission's.
            Mission mission = Mission.Current;
            int restored = 0;
            foreach (var pair in previousMortality)
            {
                if (mission == null || pair.Key.Mission != mission || !pair.Key.IsActive()) continue;
                pair.Key.SetMortalityState(pair.Value);
                restored++;
            }

            previousMortality.Clear();
            return new CoopCommandResult(true, $"HERO_COMBAT_RESTORED heroes={restored}\nLIVE_TEST_JSON=" + Snapshot());
        }
    }

    private static bool InjectMeleeHit(Agent hero, Agent victim)
    {
        // A native melee hit: Mission.CreateMeleeBlow fills the blow like this and the engine hands it to RegisterBlow.
        EquipmentIndex slot = hero.GetPrimaryWieldedItemIndex();
        if (slot == EquipmentIndex.None || hero.Equipment[slot].IsEmpty || hero.Equipment[slot].CurrentUsageItem?.IsMeleeWeapon != true)
        {
            slot = EquipmentIndex.None;
            for (EquipmentIndex candidate = EquipmentIndex.WeaponItemBeginSlot; candidate < EquipmentIndex.NumAllWeaponSlots; candidate++)
            {
                if (hero.Equipment[candidate].IsEmpty || hero.Equipment[candidate].CurrentUsageItem?.IsMeleeWeapon != true) continue;
                slot = candidate;
                break;
            }
        }

        if (slot == EquipmentIndex.None) return false;

        MissionWeapon weapon = hero.Equipment[slot];
        Vec3 direction = victim.Position - hero.Position;
        direction.z = 0f;
        if (direction.LengthSquared <= 0.0001f) return false;

        direction.Normalize();
        Blow blow = new Blow(hero.Index);
        blow.AttackType = AgentAttackType.Standard;
        blow.StrikeType = StrikeType.Swing;
        blow.DamageType = DamageTypes.Cut;
        blow.BoneIndex = victim.Monster.HeadLookDirectionBoneIndex;
        blow.VictimBodyPart = BoneBodyPartType.Head;
        blow.BaseMagnitude = 40f;
        blow.GlobalPosition = victim.Position + new Vec3(0f, 0f, 1.5f);
        blow.Direction = direction;
        blow.SwingDirection = direction;
        blow.InflictedDamage = InjectedDamage;
        blow.DamageCalculated = true;
        blow.WeaponRecord.FillAsMeleeBlow(weapon.Item, weapon.CurrentUsageItem, (int)slot,
            hero.Monster.GetBoneToAttachForItemFlags(weapon.Item.ItemFlags));

        AttackCollisionData collisionData = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
            _attackBlockedWithShield: false, _correctSideShieldBlock: false, _isAlternativeAttack: false, _isColliderAgent: true,
            _collidedWithShieldOnBack: false, _isMissile: false, _isMissileBlockedWithWeapon: false, _missileHasPhysics: false,
            _entityExists: false, _thrustTipHit: false, _missileGoneUnderWater: false, _missileGoneOutOfBorder: false,
            CombatCollisionResult.StrikeAgent, (int)slot, (int)StrikeType.Swing, (int)DamageTypes.Cut, blow.BoneIndex,
            BoneBodyPartType.Head, hero.Monster.MainHandItemBoneIndex, Agent.UsageDirection.AttackLeft, -1,
            CombatHitResultFlags.NormalHit, 0.5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, Vec3.Up, direction, blow.GlobalPosition,
            Vec3.Zero, Vec3.Zero, victim.Velocity, Vec3.Up);
        collisionData.InflictedDamage = InjectedDamage;
        collisionData.BaseMagnitude = blow.BaseMagnitude;

        victim.RegisterBlow(blow, in collisionData);
        return true;
    }

    private static bool InjectRangedHit(Mission mission, Agent hero, Agent victim)
    {
        // A native arrow hit: Mission.CreateMissileBlow fills the blow like this around a live missile. The missile
        // is fired straight up so it can't hit anything by itself; its index is what OnAgentHit resolves the weapon from.
        MissionWeapon ammo = default;
        for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
        {
            MissionWeapon candidate = hero.Equipment[slot];
            if (candidate.IsEmpty || candidate.CurrentUsageItem?.WeaponClass != WeaponClass.Arrow || candidate.Amount <= 0) continue;
            ammo = candidate;
            break;
        }

        if (ammo.IsEmpty) return false;

        Vec3 direction = victim.Position - hero.Position;
        direction.z = 0f;
        if (direction.LengthSquared <= 0.0001f) return false;

        direction.Normalize();
        MissionWeapon missileWeapon = new MissionWeapon(ammo.Item, ammo.ItemModifier, ammo.Banner);
        missileWeapon.Amount = 1;
        float speed = missileWeapon.CurrentUsageItem.MissileSpeed;
        Vec3 start = hero.GetEyeGlobalPosition() + new Vec3(0f, 0f, 1f);
        Vec3 up = Vec3.Up;
        Mat3 orientation = Mat3.CreateMat3WithForward(in up);
        Mission.Missile missile = mission.AddCustomMissile(hero, missileWeapon, start, up, orientation, speed, speed, false, null);

        Vec3 velocity = direction * speed;
        Blow blow = new Blow(hero.Index);
        blow.BlowFlag = BlowFlags.None;
        blow.Direction = direction;
        blow.SwingDirection = direction;
        blow.GlobalPosition = victim.Position + new Vec3(0f, 0f, 1.5f);
        blow.BoneIndex = victim.Monster.HeadLookDirectionBoneIndex;
        blow.StrikeType = StrikeType.Thrust;
        blow.DamageType = DamageTypes.Pierce;
        blow.VictimBodyPart = BoneBodyPartType.Head;
        blow.WeaponRecord.FillAsMissileBlow(ammo.Item, missileWeapon.CurrentUsageItem, missile.Index,
            hero.Monster.GetBoneToAttachForItemFlags(ammo.Item.ItemFlags), start, blow.GlobalPosition, velocity);
        blow.BaseMagnitude = 40f;
        blow.InflictedDamage = InjectedDamage;
        blow.DamageCalculated = true;

        AttackCollisionData collisionData = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
            _attackBlockedWithShield: false, _correctSideShieldBlock: false, _isAlternativeAttack: false, _isColliderAgent: true,
            _collidedWithShieldOnBack: false, _isMissile: true, _isMissileBlockedWithWeapon: false, _missileHasPhysics: false,
            _entityExists: false, _thrustTipHit: false, _missileGoneUnderWater: false, _missileGoneOutOfBorder: false,
            CombatCollisionResult.StrikeAgent, missile.Index, (int)StrikeType.Thrust, (int)DamageTypes.Pierce, blow.BoneIndex,
            BoneBodyPartType.Head, -1, Agent.UsageDirection.None, -1, CombatHitResultFlags.NormalHit, 0f, 0f, 0f, 0f,
            InjectedDamage, speed, 0f, 0f, Vec3.Up, direction, blow.GlobalPosition, velocity, start, victim.Velocity, Vec3.Up);
        collisionData.InflictedDamage = InjectedDamage;
        collisionData.BaseMagnitude = blow.BaseMagnitude;

        mission.OnAgentHit(victim, hero, in blow, in collisionData, false, InjectedDamage);
        return true;
    }

    private static Agent NearestEnemy(Mission mission, Agent hero, out float distance)
    {
        distance = float.MaxValue;
        if (hero.Mission != mission || !hero.IsActive() || hero.Team == null) return null;

        Agent nearest = null;
        foreach (Agent other in mission.Agents)
        {
            if (!other.IsHuman || !other.IsActive() || other.Team == null || !other.Team.IsEnemyOf(hero.Team)) continue;
            float candidate = hero.Position.Distance(other.Position);
            if (candidate >= distance) continue;
            nearest = other;
            distance = candidate;
        }

        return nearest;
    }

    public string Snapshot()
    {
        lock (gate)
        {
            Mission mission = Mission.Current;
            var heroes = mission == null
                ? new object[0]
                : mission.Agents
                    .Where(agent => agent.IsHuman && agent.IsActive() && (agent.Character as CharacterObject)?.IsHero == true)
                    .Select(agent => new
                    {
                        hero = ((CharacterObject)agent.Character).StringId,
                        health = agent.Health,
                        position = new[] { (float)Math.Round(agent.Position.x, 1), (float)Math.Round(agent.Position.y, 1) },
                        nearestEnemyMeters = NearestEnemyMeters(mission, agent),
                        weapons = Enumerable.Range((int)EquipmentIndex.WeaponItemBeginSlot, 4)
                            .Select(slot => agent.Equipment[(EquipmentIndex)slot])
                            .Select(weapon => weapon.IsEmpty ? "empty" : weapon.Item.StringId + ":" + weapon.CurrentUsageItem?.WeaponClass)
                            .ToArray(),
                    })
                    .ToArray<object>();

            return JsonConvert.SerializeObject(new
            {
                role = ModInformation.IsServer ? "server" : "client",
                heroes,
                heroHits = tallies.Values.OrderBy(tally => tally.Hero).ThenBy(tally => tally.Skill).Select(tally => new
                {
                    tally.Hero,
                    tally.Skill,
                    tally.Weapon,
                    tally.Hits,
                    tally.HitsWithoutWeapon,
                    damage = Math.Round(tally.Damage, 1),
                    expectedSkillXp = Math.Round(tally.ExpectedSkillXp, 2),
                    rawXp = Math.Round(tally.RawXp, 2),
                }).ToArray(),
                nonHeroHits,
                nonHeroHitsWithCommander,
                nonHeroDamage = Math.Round(nonHeroDamage, 1),
            });
        }
    }

    private static float NearestEnemyMeters(Mission mission, Agent hero)
    {
        NearestEnemy(mission, hero, out float distance);
        return distance == float.MaxValue ? -1f : (float)Math.Round(distance, 1);
    }

    private static string EquipBow(Agent agent)
    {
        for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
        {
            if (!agent.Equipment[slot].IsEmpty && agent.Equipment[slot].CurrentUsageItem?.WeaponClass == WeaponClass.Bow)
                return "bow=already";
        }

        ItemObject bow = MBObjectManager.Instance.GetObject<ItemObject>("hunting_bow");
        ItemObject arrows = MBObjectManager.Instance.GetObject<ItemObject>("barbed_arrows");
        if (bow == null || arrows == null) return "bow=items_missing";

        // Free weapon slots first, then the last two, so the first slots keep the hero's melee weapons.
        EquipmentIndex[] slots = Enumerable.Range((int)EquipmentIndex.WeaponItemBeginSlot, 4)
            .Select(index => (EquipmentIndex)index)
            .Where(slot => agent.Equipment[slot].IsEmpty)
            .Concat(new[] { EquipmentIndex.Weapon3, EquipmentIndex.Weapon2 })
            .Distinct()
            .Take(2)
            .ToArray();

        MissionWeapon bowWeapon = new MissionWeapon(bow, null, null);
        MissionWeapon arrowWeapon = new MissionWeapon(arrows, null, null);
        agent.EquipWeaponWithNewEntity(slots[0], ref bowWeapon);
        agent.EquipWeaponWithNewEntity(slots[1], ref arrowWeapon);
        return $"bow={slots[0]} arrows={slots[1]}";
    }

    private static CoopCommandResult Failed(string message) => new CoopCommandResult(false, message, "command_failed");

    private const int InjectedDamage = 6;
}

public sealed class HeroCombatFixtureCoopCommand : ICoopCommand
{
    private readonly IHeroCombatFixture fixture;

    public HeroCombatFixtureCoopCommand(IHeroCombatFixture fixture)
    {
        this.fixture = fixture;
    }

    public string Prefix => "coop.debug.battle";
    public string Name => "hero_combat";
    public string Description => "Injects melee and ranged hits by the heroes this client controls and tallies their hit rewards.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "prepare (bow if missing, immortal), melee or ranged (inject hits on each hero's nearest enemy), restore or state."),
        new ExpectedArgs("count", "Hits per hero for melee and ranged, default 1.", false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsServer || args.Count < 1 || args.Count > 2)
            return new CoopCommandResult(false, "Run prepare, melee, ranged, restore or state on a client.", "command_failed");

        int count = 1;
        if (args.Count == 2 && (!int.TryParse(args[1], out count) || count < 1 || count > 50))
            return new CoopCommandResult(false, "Invalid command argument value.", "command_failed");

        switch (args[0].ToLowerInvariant())
        {
            case "prepare": return fixture.Prepare();
            case "melee": return fixture.Hit(ranged: false, count);
            case "ranged": return fixture.Hit(ranged: true, count);
            case "restore": return fixture.Restore();
            case "state": return new CoopCommandResult(true, "HERO_COMBAT_STATE\nLIVE_TEST_JSON=" + fixture.Snapshot());
            default: return new CoopCommandResult(false, "Invalid command argument value.", "command_failed");
        }
    }
}
#endif
