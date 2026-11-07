using Common.Messaging;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Surrogates;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// The per-hit weapon only matters to the server for a hero attacker (weapon skill xp and
/// <c>OnHeroCombatHit</c>), so a non-hero attacker's score hit and hit reward travel without it.
/// </summary>
public class HitWeaponHeroOnlyTests : MissionTestEnvironment
{
    private const int ScoreHitXp = 40;

    private const string HeroCharacter = "hit_weapon_hero";
    private const string TroopCharacter = "hit_weapon_troop";
    private const string VictimCharacter = "hit_weapon_victim";

    public HitWeaponHeroOnlyTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ScoreHitReport_SendsWeaponOnlyForHeroAttacker(bool attackerIsHero)
    {
        var battle = SetupBattle();
        var reporter = Clients.First();

        reporter.Call(() => PublishScoreHit(reporter, battle, attackerIsHero, WeaponClass.TwoHandedSword));

        var report = Assert.Single(reporter.NetworkSentMessages.GetMessages<NetworkTroopScoreHit>());
        if (attackerIsHero)
            Assert.Equal(WeaponClass.TwoHandedSword, Assert.IsAssignableFrom<WeaponComponentData>(report.AttackerWeapon).WeaponClass);
        else
            Assert.Null(report.AttackerWeapon);
    }

    [Fact]
    public void ScoreHit_FromHeroAttacker_StillRaisesHeroCombatHitWithTheWeapon()
    {
        var battle = SetupBattle();
        var reporter = Clients.First();
        var heroHits = new List<(WeaponClass? WeaponClass, bool IsFatal)>();
        int contributionBefore = 0;
        int contributionAfter = 0;

        Server.Call(() =>
        {
            CampaignEvents.OnHeroCombatHitEvent.AddNonSerializedListener(this,
                (attacker, attacked, party, weapon, isFatal, xp) => heroHits.Add((weapon?.WeaponClass, isFatal)));
            contributionBefore = ServerParty(battle).ContributionToBattle;
        });

        try
        {
            reporter.Call(() => PublishScoreHit(reporter, battle, attackerIsHero: true, WeaponClass.TwoHandedAxe));

            Server.Call(() => contributionAfter = ServerParty(battle).ContributionToBattle);
        }
        finally
        {
            Server.Call(() => CampaignEvents.OnHeroCombatHitEvent.ClearListeners(this));
        }

        var hit = Assert.Single(heroHits);
        Assert.Equal(WeaponClass.TwoHandedAxe, hit.WeaponClass);
        Assert.True(hit.IsFatal);
        Assert.Equal(ScoreHitXp, contributionAfter - contributionBefore);
    }

    [Fact]
    public void ScoreHit_FromNonHeroAttacker_AccountsTheSameWithNullWeaponAsWithOne()
    {
        var battle = SetupBattle();
        var reporter = Clients.First();
        var heroHits = new List<WeaponClass?>();
        var deltas = new List<(int Contribution, int Xp)>();

        Server.Call(() => CampaignEvents.OnHeroCombatHitEvent.AddNonSerializedListener(this,
            (attacker, attacked, party, weapon, isFatal, xp) => heroHits.Add(weapon?.WeaponClass)));

        try
        {
            foreach (var weaponClass in new WeaponClass?[] { WeaponClass.OneHandedSword, null })
            {
                (int Contribution, int Xp) before = default;
                Server.Call(() => before = ServerTroopTotals(battle));

                // A weapon-carrying report, as the message looked before the gate, then the gated null one.
                SendScoreHit(reporter, battle, TroopCharacter, weaponClass);

                Server.Call(() =>
                {
                    var after = ServerTroopTotals(battle);
                    deltas.Add((after.Contribution - before.Contribution, after.Xp - before.Xp));
                });
            }
        }
        finally
        {
            Server.Call(() => CampaignEvents.OnHeroCombatHitEvent.ClearListeners(this));
        }

        Assert.Equal(2, deltas.Count);
        Assert.Equal((ScoreHitXp, ScoreHitXp), deltas[0]);
        Assert.Equal(deltas[0], deltas[1]);
        Assert.Empty(heroHits);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HitReward_OnServer_PassesTheReceivedWeaponToSkillLeveling(bool attackerIsHero)
    {
        var battle = SetupBattle();
        var reporter = Clients.First();
        var skills = new Mock<ISkillLevelingManager>();
        ISkillLevelingManager original = null!;

        Server.Call(() =>
        {
            original = Campaign.Current.SkillLevelingManager;
            Campaign.Current.SkillLevelingManager = skills.Object;
        });

        try
        {
            reporter.Call(() => PublishHitReward(reporter, battle, attackerIsHero, WeaponClass.Dagger));
        }
        finally
        {
            Server.Call(() => Campaign.Current.SkillLevelingManager = original);
        }

        var call = Assert.Single(skills.Invocations, invocation => invocation.Method.Name == nameof(ISkillLevelingManager.OnCombatHit));
        var affector = (CharacterObject)call.Arguments[0];
        var weapon = (WeaponComponentData?)call.Arguments[6];

        Server.Call(() => Assert.Same(Server.GetRegisteredObject<CharacterObject>(attackerIsHero ? HeroCharacter : TroopCharacter), affector));
        Assert.Equal(attackerIsHero, affector.IsHero);
        if (attackerIsHero)
            Assert.Equal(WeaponClass.Dagger, Assert.IsAssignableFrom<WeaponComponentData>(weapon).WeaponClass);
        else
            Assert.Null(weapon);
    }

    [Fact]
    public void NetworkTroopScoreHit_RoundTripsNullWeapon()
    {
        var original = new NetworkTroopScoreHit("party", "attacker", "victim", damage: 30, isFatal: true, isSimulatedHit: false, attackerWeapon: null);

        var result = RoundTrip(original);

        Assert.Equal("party", result.MapEventPartyId);
        Assert.Equal("attacker", result.AttackingTroopId);
        Assert.Equal("victim", result.AttackedTroopId);
        Assert.Equal(30, result.Damage);
        Assert.True(result.IsFatal);
        Assert.Null(result.AttackerWeapon);
    }

    [Fact]
    public void NetworkBattleHitReward_RoundTripsNullWeapon()
    {
        var original = new NetworkBattleHitReward(
            "mapEvent", "affected", "affector", captainId: null, heroId: null,
            BattleSideEnum.Defender, BattleSideEnum.Attacker, isAgentMounted: false,
            lastSpeedBonus: 0.5f, lastShotDifficulty: 1.5f, isSiegeEngineHit: false,
            lastAttackerWeapon: null, AgentAttackType.Standard,
            hitpointRatio: 0.25f, damageAmount: 17f, affectorPartyId: "party",
            isSneakAttack: false, affectedAgentHealth: 40f, isAffectorUnderCommand: true);

        var result = RoundTrip(original);

        Assert.Equal("mapEvent", result.MapEventId);
        Assert.Equal("affector", result.AffectorCharacterId);
        Assert.Equal("party", result.AffectorPartyId);
        Assert.Equal(17f, result.DamageAmount);
        Assert.Equal(1.5f, result.LastShotDifficulty);
        Assert.True(result.IsAffectorUnderCommand);
        Assert.Null(result.LastAttackerWeapon);
    }

    private static T RoundTrip<T>(T original) where T : IMessage
    {
        new SurrogateCollection();
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        MessagePacket packet = MessagePacket.Create(original, serializer);
        return Assert.IsType<T>(serializer.Deserialize<IMessage>(packet.Data));
    }

    private sealed record Battle(string MapEventId, string MobilePartyId, string PartyId, string HeroId);

    /// <summary>
    /// A coop battle whose attacker-side party holds a hero troop and a regular troop, with a victim
    /// character, all registered on the server and every client.
    /// </summary>
    private Battle SetupBattle()
    {
        var (mapEventId, partyIds, heroIds) = SetupCoopBattleWithHeroes("attacker", "defender");
        EnsureServerCampaignModels();

        foreach (var instance in new[] { Server }.Concat(Clients))
        {
            instance.Call(() =>
            {
                foreach (var id in new[] { HeroCharacter, TroopCharacter, VictimCharacter })
                {
                    var character = instance.CreateRegisteredObject<CharacterObject>(id);
                    character.Culture = ObjectHelper.SkipConstructor<CultureObject>();
                }
            });
        }

        string? mapEventPartyId = null;
        var heroId = heroIds[0];
        foreach (var instance in new[] { Server }.Concat(Clients))
        {
            instance.Call(() =>
            {
                var hero = instance.GetRegisteredObject<Hero>(heroId);
                using (new AllowedThread())
                    instance.GetRegisteredObject<CharacterObject>(HeroCharacter).HeroObject = hero;
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var party = mapEvent.AttackerSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(party, out mapEventPartyId));

            party.Party.MemberRoster.AddToCounts(Server.GetRegisteredObject<CharacterObject>(HeroCharacter), 1);
            party.Party.MemberRoster.AddToCounts(Server.GetRegisteredObject<CharacterObject>(TroopCharacter), 1);
            party.Update();
        });

        Assert.NotNull(mapEventPartyId);
        return new Battle(mapEventId, partyIds[0], mapEventPartyId!, heroIds[0]);
    }

    private MapEventParty ServerParty(Battle battle)
    {
        Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(battle.PartyId, out var party));
        return party;
    }

    private (int Contribution, int Xp) ServerTroopTotals(Battle battle)
    {
        var party = ServerParty(battle);
        var troop = Server.GetRegisteredObject<CharacterObject>(TroopCharacter);
        return (party.ContributionToBattle, party.Troops.Where(element => element.Troop == troop).Sum(element => element.XpGained));
    }

    /// <summary>Publishes the score hit the way the client that applied the blow does.</summary>
    private void PublishScoreHit(EnvironmentInstance client, Battle battle, bool attackerIsHero, WeaponClass weaponClass)
    {
        Assert.True(client.ObjectManager.TryGetObject<MapEventParty>(battle.PartyId, out var party));

        client.Resolve<IMessageBroker>().Publish(this, new OnTroopScoreHitAttempted(
            party,
            client.GetRegisteredObject<CharacterObject>(attackerIsHero ? HeroCharacter : TroopCharacter),
            client.GetRegisteredObject<CharacterObject>(VictimCharacter),
            damage: 30,
            isFatal: true,
            isSimulatedHit: false,
            new WeaponComponentData(null, weaponClass, default)));
    }

    /// <summary>Sends a score hit for <paramref name="attackerCharacter"/> straight onto the wire, as a build without the gate would.</summary>
    private void SendScoreHit(EnvironmentInstance client, Battle battle, string attackerCharacter, WeaponClass? weaponClass)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetId(client.GetRegisteredObject<CharacterObject>(attackerCharacter), out string attackerId));
            Assert.True(client.ObjectManager.TryGetId(client.GetRegisteredObject<CharacterObject>(VictimCharacter), out string victimId));

            client.Resolve<Common.Network.INetwork>().SendAll(new NetworkTroopScoreHit(
                battle.PartyId, attackerId, victimId, damage: 30, isFatal: true, isSimulatedHit: false,
                weaponClass.HasValue ? new WeaponComponentData(null, weaponClass.Value, default) : null));
        });
    }

    private void PublishHitReward(EnvironmentInstance client, Battle battle, bool attackerIsHero, WeaponClass weaponClass)
    {
        Assert.True(client.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent));
        var party = client.GetRegisteredObject<MobileParty>(battle.MobilePartyId);

        client.Resolve<IMessageBroker>().Publish(this, new BattleHitReward(
            mapEvent,
            client.GetRegisteredObject<CharacterObject>(VictimCharacter),
            client.GetRegisteredObject<CharacterObject>(attackerIsHero ? HeroCharacter : TroopCharacter),
            captain: null,
            hero: null,
            BattleSideEnum.Defender,
            BattleSideEnum.Attacker,
            isAgentMounted: false,
            lastSpeedBonus: 0f,
            lastShotDifficulty: 0f,
            isSiegeEngineHit: false,
            new WeaponComponentData(null, weaponClass, default),
            AgentAttackType.Standard,
            hitpointRatio: 0.5f,
            damageAmount: 30f,
            party.Party,
            isSneakAttack: false,
            affectedAgentHealth: 0f,
            isAffectorUnderCommand: false));
    }

    /// <summary>
    /// The harness boots a <see cref="Campaign"/> without the campaign-start flow that would populate
    /// <c>Campaign.Models</c>, so give the server the xp model the native scorer runs through.
    /// </summary>
    private void EnsureServerCampaignModels()
    {
        Server.Call(() =>
        {
            var gameModels = new GameModels(new List<GameModel>
            {
                new FixedCombatXpModel(),
                new DefaultMilitaryPowerModel(),
                new DefaultCharacterStatsModel(),
            });
            Server.GameInstance.Game._gameModelManagers[typeof(GameModels)] = gameModels;
            Campaign.Current._gameModels = gameModels;
        });
    }

    private sealed class FixedCombatXpModel : CombatXpModel
    {
        public override float CaptainRadius => 0f;

        public override SkillObject GetSkillForWeapon(WeaponComponentData weapon, bool isSiegeEngineHit) => null;

        public override ExplainedNumber GetXpFromHit(
            CharacterObject attackerTroop,
            CharacterObject captain,
            CharacterObject attackedTroop,
            PartyBase attackerParty,
            int damage,
            bool isFatal,
            MissionTypeEnum missionType) =>
            new ExplainedNumber(ScoreHitXp);

        public override float GetXpMultiplierFromShotDifficulty(float shotDifficulty) => 1f;
    }
}
