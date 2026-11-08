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
    CoopCommandResult Start(bool giveBow);
    CoopCommandResult Chase();
    CoopCommandResult Stop();
    string Snapshot();
}

/// <summary>
/// DEBUG cheat for #4499: lets the heroes this client controls fight on their own (AI controller), optionally with a
/// bow, and tallies each hero hit reward next to the skill xp vanilla would give for it, so the server's xp can be
/// compared with it.
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
    private readonly Dictionary<Agent, AgentControllerType> previousControllers = new Dictionary<Agent, AgentControllerType>();
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

    public CoopCommandResult Start(bool giveBow)
    {
        Mission mission = Mission.Current;
        if (mission == null || !ContainerProvider.TryResolve<INetworkAgentRegistry>(out var registry))
            return Failed("No active coop battle mission");

        Agent[] heroes = mission.Agents
            .Where(agent => agent.IsHuman && agent.IsActive() && (agent.Character as CharacterObject)?.IsHero == true
                && registry.IsLocallyControlled(agent))
            .ToArray();
        if (heroes.Length == 0)
            return Failed("This client controls no active hero agent");

        lock (gate)
        {
            previousControllers.Clear();
        }

        var notes = new List<string>();
        foreach (Agent agent in heroes)
        {
            if (giveBow) notes.Add(EquipBow(agent));

            agent.SetMortalityState(Agent.MortalityState.Immortal);
            if (agent.Formation == null)
                mission.PlayerTeam?.GetFormation(FormationClass.Infantry)?.AddUnit(agent);

            lock (gate)
            {
                if (agent.Controller != AgentControllerType.AI)
                {
                    previousControllers[agent] = agent.Controller;
                    agent.Controller = AgentControllerType.AI;
                }
            }

            agent.SetIsAIPaused(false);
            agent.ClearTargetFrame();
        }

        return new CoopCommandResult(true, $"HERO_COMBAT_STARTED heroes={heroes.Length} bow={giveBow} {string.Join(" ", notes)}\nLIVE_TEST_JSON=" + Snapshot());
    }

    public CoopCommandResult Chase()
    {
        Mission mission = Mission.Current;
        if (mission == null) return Failed("No active coop battle mission");

        int chasing = 0;
        lock (gate)
        {
            foreach (Agent agent in previousControllers.Keys)
            {
                if (agent.Mission != mission || !agent.IsActive() || agent.Team == null) continue;

                Agent nearest = null;
                float nearestDistance = float.MaxValue;
                foreach (Agent other in mission.Agents)
                {
                    if (!other.IsHuman || !other.IsActive() || other.Team == null || !other.Team.IsEnemyOf(agent.Team)) continue;
                    float distance = agent.Position.Distance(other.Position);
                    if (distance >= nearestDistance) continue;
                    nearest = other;
                    nearestDistance = distance;
                }

                Vec3 direction = nearest == null ? Vec3.Zero : nearest.Position - agent.Position;
                direction.z = 0f;
                if (nearest == null || direction.LengthSquared <= 0.0001f) continue;

                direction.Normalize();
                Vec2 target = nearest.Position.AsVec2;
                agent.SetTargetPositionAndDirection(in target, in direction);
                chasing++;
            }
        }

        return new CoopCommandResult(true, $"HERO_COMBAT_CHASE heroes={chasing}");
    }

    public CoopCommandResult Stop()
    {
        lock (gate)
        {
            // Agents of an earlier mission are gone with it; only touch the current mission's.
            Mission mission = Mission.Current;
            int restored = 0;
            foreach (var pair in previousControllers)
            {
                if (mission == null || pair.Key.Mission != mission || !pair.Key.IsActive()) continue;
                pair.Key.ClearTargetFrame();
                pair.Key.Controller = pair.Value;
                restored++;
            }

            previousControllers.Clear();
            return new CoopCommandResult(true, $"HERO_COMBAT_STOPPED restored={restored}\nLIVE_TEST_JSON=" + Snapshot());
        }
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
                        controller = agent.Controller.ToString(),
                        health = agent.Health,
                        position = new[] { (float)Math.Round(agent.Position.x, 1), (float)Math.Round(agent.Position.y, 1) },
                        speed = (float)Math.Round(agent.GetCurrentVelocity().Length, 2),
                        aiPaused = agent.IsPaused,
                        wielded = agent.GetPrimaryWieldedItemIndex().ToString(),
                        formation = agent.Formation == null
                            ? "none"
                            : $"{agent.Formation.FormationIndex} units={agent.Formation.CountOfUnits} order={agent.Formation.GetReadonlyMovementOrderReference().OrderType}",
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
        float nearest = float.MaxValue;
        foreach (Agent other in mission.Agents)
        {
            if (!other.IsHuman || !other.IsActive() || other.Team == null || hero.Team == null || !other.Team.IsEnemyOf(hero.Team)) continue;
            nearest = Math.Min(nearest, hero.Position.Distance(other.Position));
        }

        return nearest == float.MaxValue ? -1f : (float)Math.Round(nearest, 1);
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
    public string Description => "Lets the heroes this client controls fight under AI control and tallies their hit rewards.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("action", "start, start_bow (adds a hunting bow and arrows first), chase (walk to the nearest enemy), stop or state."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (ModInformation.IsServer || args.Count != 1)
            return new CoopCommandResult(false, "Run start, start_bow, stop or state on a client.", "command_failed");

        switch (args[0].ToLowerInvariant())
        {
            case "start": return fixture.Start(giveBow: false);
            case "start_bow": return fixture.Start(giveBow: true);
            case "chase": return fixture.Chase();
            case "stop": return fixture.Stop();
            case "state": return new CoopCommandResult(true, "HERO_COMBAT_STATE\nLIVE_TEST_JSON=" + fixture.Snapshot());
            default: return new CoopCommandResult(false, "Invalid command argument value.", "command_failed");
        }
    }
}
#endif
