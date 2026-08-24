using Autofac;
using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Commands;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.SiegeEngines;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using HarmonyLib;
using Newtonsoft.Json;
using SandBox.View.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using static TaleWorlds.CampaignSystem.Army;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.SiegeEvents.Commands;

public class SiegeDebugCommand
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeDebugCommand>();
    private static PrisonerPromptFixture prisonerPromptFixture;

    private sealed class PrisonerPromptFixture
    {
        public string ControllerId;
        public Settlement Settlement;
        public Hero OriginalOwner;
        public MobileParty PlayerParty;
        public MobileParty ArmyLeader;
        public PartyBehaviorUpdateData PlayerBehavior;
        public PartyBehaviorUpdateData LeaderBehavior;
        public IFaction AttackerFaction;
        public IFaction DefenderFaction;
        public bool WasAtWar;
        public Army Army;
        public PrisonerPromptPartySnapshot[] PartySnapshots;
        public PrisonerPromptHeroSnapshot[] HeroSnapshots;
        public PrisonerPromptClanSnapshot[] ClanSnapshots;
        public Hero Governor;
        public Clan LastCapturedBy;
        public bool HadGarrison;
        public float Prosperity;
        public float Loyalty;
        public float Security;
        public float FoodStocks;
    }

    private sealed class PrisonerPromptPartySnapshot
    {
        public PartyBase Party;
        public TroopRosterElement[] MemberRoster;
        public TroopRosterElement[] PrisonRoster;
        public ItemRosterElement[] Items;
        public Hero LeaderHero;
        public bool WasActive;
        public float RecentEventsMorale;
        public int PartyTradeGold;
        public CampaignVec2 Position;
        public bool IsSettlementGarrison;
        public bool IsSettlementMilitia;
        public string StringId;
    }

    private sealed class PrisonerPromptHeroSnapshot
    {
        public Hero Hero;
        public Hero.CharacterStates State;
        public MobileParty Party;
        public PartyBase PrisonerParty;
        public int HitPoints;
        public int Gold;
        public KillCharacterAction.KillCharacterActionDetail DeathMark;
        public Hero DeathMarkKillerHero;
        public Dictionary<SkillObject, int> SkillLevels;
        public Dictionary<SkillObject, float> SkillXps;
        public int TotalXp;
        public int UnspentFocusPoints;
        public int UnspentAttributePoints;
    }

    private sealed class PrisonerPromptClanSnapshot
    {
        public Clan Clan;
        public float Influence;
        public float Renown;
        public int Tier;
    }

    [CommandLineArgumentFunction("prisoner_prompt_fixture_start", "coop.debug.siege")]
    public static string StartPrisonerPromptFixture(List<string> args)
    {
        const string usage = "Usage: coop.debug.siege.prisoner_prompt_fixture_start <controllerId> <settlementId>";
        if (!ModInformation.IsServer) return "This command can only be used by the server";
        if (args.Count != 2) return usage;
        if (prisonerPromptFixture != null) return "A prisoner-prompt siege fixture is already active.";

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return "Unable to resolve prisoner-prompt fixture services.";

        if (!playerManager.TryGetPlayer(args[0], out var player) || !playerManager.IsConnected(player) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
            return $"No connected player has controller id {args[0]}.";
        if (!objectManager.TryGetObject<Settlement>(args[1], out var settlement))
            return $"Settlement with id {args[1]} not found.";
        if (!settlement.IsFortification || settlement.OwnerClan?.Leader == null)
            return $"{settlement.Name} must be an owned fortification.";
        if (playerParty.MapFaction is not Kingdom attackerKingdom ||
            settlement.MapFaction == null || settlement.MapFaction == attackerKingdom)
            return "The player party must belong to a kingdom hostile to the target owner.";
        if (playerParty.MapEvent != null || playerParty.BesiegerCamp != null ||
            playerParty.CurrentSettlement != null || playerParty.Army != null || settlement.SiegeEvent != null)
            return "The player party and target must be outside an army, settlement, siege, and map event.";

        var armyLeader = MobileParty.AllLordParties
            .Where(party => party.IsActive && !party.IsPlayerParty() && party.MapFaction == attackerKingdom &&
                party.LeaderHero != null && party.MapEvent == null && party.CurrentSettlement == null &&
                party.BesiegerCamp == null && party.Army == null && party.MemberRoster.TotalHealthyCount > 0)
            .OrderByDescending(party => party.Party.CalculateCurrentStrength())
            .FirstOrDefault();
        if (armyLeader == null)
            return $"No available {attackerKingdom.Name} lord party can lead the fixture army.";
        if (!behaviorSnapshot.TryCreate(playerParty, out var playerBehavior) ||
            !behaviorSnapshot.TryCreate(armyLeader, out var leaderBehavior))
            return "Unable to capture the original party behavior.";

        PrisonerPromptPartySnapshot[] partySnapshots;
        PrisonerPromptHeroSnapshot[] heroSnapshots;
        PrisonerPromptClanSnapshot[] clanSnapshots;
        try
        {
            partySnapshots = CapturePrisonerPromptParties(playerParty, armyLeader, settlement);
            heroSnapshots = CapturePrisonerPromptHeroes(partySnapshots, settlement);
            clanSnapshots = CapturePrisonerPromptClans(heroSnapshots);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to capture prisoner-prompt siege fixture");
            return "Unable to capture the original combat state: " + ex.Message;
        }

        var fixture = new PrisonerPromptFixture
        {
            ControllerId = args[0],
            Settlement = settlement,
            OriginalOwner = settlement.OwnerClan.Leader,
            PlayerParty = playerParty,
            ArmyLeader = armyLeader,
            PlayerBehavior = playerBehavior,
            LeaderBehavior = leaderBehavior,
            AttackerFaction = attackerKingdom,
            DefenderFaction = settlement.MapFaction,
            WasAtWar = attackerKingdom.IsAtWarWith(settlement.MapFaction),
            PartySnapshots = partySnapshots,
            HeroSnapshots = heroSnapshots,
            ClanSnapshots = clanSnapshots,
            Governor = settlement.Town.Governor,
            LastCapturedBy = settlement.Town.LastCapturedBy,
            HadGarrison = settlement.Town.GarrisonParty != null,
            Prosperity = settlement.Town.Prosperity,
            Loyalty = settlement.Town.Loyalty,
            Security = settlement.Town.Security,
            FoodStocks = settlement.Town.FoodStocks,
        };
        prisonerPromptFixture = fixture;

        try
        {
            if (!fixture.WasAtWar)
                DeclareWarAction.ApplyByDefault(fixture.AttackerFaction, fixture.DefenderFaction);

            armyLeader.Position = settlement.GatePosition;
            playerParty.Position = settlement.GatePosition;
            attackerKingdom.CreateArmy(armyLeader.LeaderHero, settlement, ArmyTypes.Besieger);
            fixture.Army = armyLeader.Army;
            if (fixture.Army == null)
                throw new InvalidOperationException("Vanilla did not create the fixture army.");

            playerParty.Army = fixture.Army;
            fixture.Army.AddPartyToMergedParties(playerParty);
            armyLeader.SetMoveBesiegeSettlement(settlement, MobileParty.NavigationType.Default);
            siegeEventInterface.StartSiegeEvent(armyLeader, settlement);
            if (playerParty.BesiegerCamp == null)
                siegeEventInterface.JoinSiegeCamp(playerParty, settlement);

            if (settlement.SiegeEvent == null || playerParty.Army != fixture.Army ||
                fixture.Army.LeaderParty == playerParty)
                throw new InvalidOperationException("The army siege fixture did not reach its required state.");

            objectManager.TryGetId(fixture.Army, out string armyId);
            return "Prisoner-prompt army siege fixture started." + Environment.NewLine +
                "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
                {
                    success = true,
                    controllerId = fixture.ControllerId,
                    settlementId = settlement.StringId,
                    armyId,
                    leaderPartyId = armyLeader.StringId,
                    playerPartyId = playerParty.StringId,
                    playerIsArmyMember = playerParty.Army == fixture.Army,
                    playerIsNonLeaderMember = fixture.Army.LeaderParty != playerParty,
                    siegeActive = settlement.SiegeEvent != null,
                    warDeclaredByFixture = !fixture.WasAtWar,
                });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to start prisoner-prompt siege fixture");
            bool restored = TryRestorePrisonerPromptFixture(
                fixture,
                behaviorSnapshot,
                siegeEventInterface,
                out var restoreError);
            if (restored)
                prisonerPromptFixture = null;

            return "Failed to start prisoner-prompt siege fixture: " + ex.Message +
                (restored ? string.Empty : ". Restore is still required: " + restoreError);
        }
    }

    [CommandLineArgumentFunction("prisoner_prompt_fixture_state", "coop.debug.siege")]
    public static string PrisonerPromptFixtureState(List<string> args)
    {
        const string usage = "Usage: coop.debug.siege.prisoner_prompt_fixture_state <controllerId> <settlementId>";
        if (!ModInformation.IsServer) return "This command can only be used by the server";
        if (args.Count != 2) return usage;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(args[0], out var player) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty) ||
            !objectManager.TryGetObject<Settlement>(args[1], out var settlement))
            return "Unable to resolve prisoner-prompt fixture state.";

        var army = playerParty.Army;
        string armyId = null;
        if (army != null)
            objectManager.TryGetId(army, out armyId);
        return "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            success = true,
            fixtureActive = prisonerPromptFixture != null,
            controllerId = args[0],
            settlementId = settlement.StringId,
            settlementOwnerId = settlement.OwnerClan?.StringId,
            playerPartyId = playerParty.StringId,
            playerMapEventActive = playerParty.MapEvent != null,
            playerBesieger = settlement.SiegeEvent != null &&
                playerParty.BesiegerCamp == settlement.SiegeEvent.BesiegerCamp,
            armyId,
            playerIsArmyMember = army != null,
            playerIsNonLeaderMember = army != null && army.LeaderParty != playerParty,
            armyLeaderPartyId = army?.LeaderParty?.StringId,
            siegeActive = settlement.SiegeEvent != null,
            atWar = playerParty.MapFaction?.IsAtWarWith(settlement.MapFaction) == true,
            playerX = playerParty.Position.X,
            playerY = playerParty.Position.Y,
        });
    }

    [CommandLineArgumentFunction("prisoner_prompt_fixture_restore", "coop.debug.siege")]
    public static string RestorePrisonerPromptFixture(List<string> args)
    {
        if (!ModInformation.IsServer) return "This command can only be used by the server";
        if (args.Count != 0) return "Usage: coop.debug.siege.prisoner_prompt_fixture_restore";
        var fixture = prisonerPromptFixture;
        if (fixture == null) return "No prisoner-prompt siege fixture is active.";
        if (fixture.PlayerParty.MapEvent != null || fixture.ArmyLeader.MapEvent != null)
            return "The fixture battle must finish before restoration.";
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
            !ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
            return "Unable to resolve prisoner-prompt fixture restore services.";

        if (!TryRestorePrisonerPromptFixture(fixture, behaviorSnapshot, siegeEventInterface, out var error))
            return "Failed to restore prisoner-prompt siege fixture: " + error;

        prisonerPromptFixture = null;
        return "Prisoner-prompt army siege fixture restored." + Environment.NewLine +
            "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
            {
                success = true,
                restored = true,
                controllerId = fixture.ControllerId,
                settlementId = fixture.Settlement.StringId,
                armyRemoved = fixture.PlayerParty.Army == null && fixture.ArmyLeader.Army == null,
                siegeRemoved = fixture.Settlement.SiegeEvent == null,
                ownerRestored = fixture.Settlement.OwnerClan == fixture.OriginalOwner.Clan,
                warRestored = fixture.WasAtWar ||
                    !fixture.AttackerFaction.IsAtWarWith(fixture.DefenderFaction),
                combatStateRestored = IsPrisonerPromptCombatStateRestored(fixture),
            });
    }

    private static PrisonerPromptPartySnapshot[] CapturePrisonerPromptParties(
        MobileParty playerParty,
        MobileParty armyLeader,
        Settlement settlement)
    {
        return new[]
            {
                playerParty.Party,
                armyLeader.Party,
                settlement.Party,
                settlement.Town.GarrisonParty?.Party,
            }
            .Concat(settlement.Parties.Select(party => party.Party))
            .Where(party => party != null)
            .Distinct()
            .Select(party => new PrisonerPromptPartySnapshot
            {
                Party = party,
                MemberRoster = party.MemberRoster.GetTroopRoster().ToArray(),
                PrisonRoster = party.PrisonRoster.GetTroopRoster().ToArray(),
                Items = party.ItemRoster.ToArray(),
                LeaderHero = party.LeaderHero,
                WasActive = party.MobileParty?.IsActive == true,
                RecentEventsMorale = party.MobileParty?.RecentEventsMorale ?? 0f,
                PartyTradeGold = party.MobileParty?.PartyTradeGold ?? 0,
                Position = party.MobileParty?.Position ?? default,
                IsSettlementGarrison = settlement.Town.GarrisonParty != null &&
                    party.MobileParty == settlement.Town.GarrisonParty,
                IsSettlementMilitia = settlement.MilitiaPartyComponent?.MobileParty != null &&
                    party.MobileParty == settlement.MilitiaPartyComponent.MobileParty,
                StringId = party.MobileParty?.StringId,
            })
            .ToArray();
    }

    private static PrisonerPromptHeroSnapshot[] CapturePrisonerPromptHeroes(
        PrisonerPromptPartySnapshot[] partySnapshots,
        Settlement settlement)
    {
        return partySnapshots
            .SelectMany(snapshot => snapshot.MemberRoster
                .Concat(snapshot.PrisonRoster)
                .Select(element => element.Character.HeroObject)
                .Concat(new[] { snapshot.LeaderHero }))
            .Concat(new[] { settlement.Town.Governor })
            .Where(hero => hero != null)
            .Distinct()
            .Select(hero => new PrisonerPromptHeroSnapshot
            {
                Hero = hero,
                State = hero.HeroState,
                Party = hero.PartyBelongedTo,
                PrisonerParty = hero.PartyBelongedToAsPrisoner,
                HitPoints = hero.HitPoints,
                Gold = hero.Gold,
                DeathMark = hero.DeathMark,
                DeathMarkKillerHero = hero.DeathMarkKillerHero,
                SkillLevels = Skills.All.ToDictionary(skill => skill, hero.GetSkillValue),
                SkillXps = hero.HeroDeveloper == null
                    ? null
                    : Skills.All.ToDictionary(skill => skill, hero.HeroDeveloper.GetSkillXp),
                TotalXp = hero.HeroDeveloper?._totalXp ?? 0,
                UnspentFocusPoints = hero.HeroDeveloper?.UnspentFocusPoints ?? 0,
                UnspentAttributePoints = hero.HeroDeveloper?.UnspentAttributePoints ?? 0,
            })
            .ToArray();
    }

    private static PrisonerPromptClanSnapshot[] CapturePrisonerPromptClans(
        PrisonerPromptHeroSnapshot[] heroSnapshots)
    {
        return heroSnapshots
            .Select(snapshot => snapshot.Hero.Clan)
            .Where(clan => clan != null)
            .Distinct()
            .Select(clan => new PrisonerPromptClanSnapshot
            {
                Clan = clan,
                Influence = clan._influence,
                Renown = clan.Renown,
                Tier = clan._tier,
            })
            .ToArray();
    }

    private static bool TryRestorePrisonerPromptFixture(
        PrisonerPromptFixture fixture,
        IMobilePartyBehaviorSnapshot behaviorSnapshot,
        ISiegeEventInterface siegeEventInterface,
        out string error)
    {
        error = string.Empty;
        try
        {
            var camp = fixture.Settlement.SiegeEvent?.BesiegerCamp;
            if (camp != null)
            {
                foreach (var party in camp._besiegerParties.ToArray())
                    siegeEventInterface.BreakSiege(party);
            }

            if (fixture.Army != null && fixture.PlayerParty.Army == fixture.Army)
                ArmyPatches.RemoveMobilePartyInArmyImmediate(
                    fixture.PlayerParty,
                    fixture.Army,
                    null);
            if (fixture.Army != null && fixture.ArmyLeader.Army == fixture.Army)
                DisbandArmyAction.ApplyByObjectiveFinished(fixture.Army);

            if (fixture.Settlement.OwnerClan != fixture.OriginalOwner.Clan)
                ChangeOwnerOfSettlementAction.ApplyByGift(fixture.Settlement, fixture.OriginalOwner);
            if (!fixture.WasAtWar && fixture.AttackerFaction.IsAtWarWith(fixture.DefenderFaction))
                MakePeaceAction.Apply(fixture.AttackerFaction, fixture.DefenderFaction);

            RestorePrisonerPromptGarrison(fixture);
            RestorePrisonerPromptMilitia(fixture);

            fixture.Settlement.Town.Prosperity = fixture.Prosperity;
            fixture.Settlement.Town.Loyalty = fixture.Loyalty;
            fixture.Settlement.Town.Security = fixture.Security;
            fixture.Settlement.Town.FoodStocks = fixture.FoodStocks;

            foreach (var hero in fixture.HeroSnapshots)
                RestorePrisonerPromptHeroProgression(hero);
            foreach (var party in fixture.PartySnapshots)
                RestorePrisonerPromptParty(fixture, party);
            foreach (var hero in fixture.HeroSnapshots)
                RestorePrisonerPromptHeroMembership(fixture, hero);
            foreach (var clan in fixture.ClanSnapshots)
                RestorePrisonerPromptClan(clan);

            fixture.Settlement.Town.Governor = fixture.Governor;
            fixture.Settlement.Town.LastCapturedBy = fixture.LastCapturedBy;

            fixture.PlayerParty.Position = fixture.PlayerBehavior.PartyPosition;
            fixture.ArmyLeader.Position = fixture.LeaderBehavior.PartyPosition;
            bool playerRestored = behaviorSnapshot.TryApply(
                fixture.PlayerParty,
                fixture.PlayerBehavior,
                out _);
            bool leaderRestored = behaviorSnapshot.TryApply(
                fixture.ArmyLeader,
                fixture.LeaderBehavior,
                out _);
            foreach (var party in fixture.PartySnapshots
                .Select(snapshot => ResolvePrisonerPromptParty(fixture, snapshot)?.MobileParty)
                .Where(party => party?.IsActive == true))
            {
                PublishPrisonerPromptForcedPosition(party);
            }
            bool combatStateRestored = IsPrisonerPromptCombatStateRestored(fixture);
            if (!playerRestored || !leaderRestored || fixture.Settlement.SiegeEvent != null ||
                fixture.PlayerParty.Army != null || fixture.ArmyLeader.Army != null ||
                !combatStateRestored)
            {
                error = "one or more fixture invariants were not restored";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to restore prisoner-prompt siege fixture");
            error = ex.Message;
            return false;
        }
    }

    private static void RestorePrisonerPromptHeroProgression(PrisonerPromptHeroSnapshot snapshot)
    {
        if (snapshot.Hero.IsPrisoner)
            EndCaptivityAction.ApplyByPeace(snapshot.Hero);

        snapshot.Hero.DeathMark = snapshot.DeathMark;
        snapshot.Hero.DeathMarkKillerHero = snapshot.DeathMarkKillerHero;
        snapshot.Hero.HitPoints = snapshot.HitPoints;
        snapshot.Hero.Gold = snapshot.Gold;
        snapshot.Hero.ChangeState(snapshot.State);
        foreach (var skill in snapshot.SkillLevels)
            snapshot.Hero.SetSkillValue(skill.Key, skill.Value);

        if (snapshot.Hero.HeroDeveloper == null || snapshot.SkillXps == null)
            return;

        foreach (var skillXp in snapshot.SkillXps)
            snapshot.Hero.HeroDeveloper.SetSkillXp(skillXp.Key, skillXp.Value);
        snapshot.Hero.HeroDeveloper._totalXp = snapshot.TotalXp;
        snapshot.Hero.HeroDeveloper.UnspentFocusPoints = snapshot.UnspentFocusPoints;
        snapshot.Hero.HeroDeveloper.UnspentAttributePoints = snapshot.UnspentAttributePoints;
    }

    private static void RestorePrisonerPromptGarrison(PrisonerPromptFixture fixture)
    {
        var snapshot = fixture.PartySnapshots.SingleOrDefault(party => party.IsSettlementGarrison);
        if (snapshot == null)
        {
            if (fixture.Settlement.Town.GarrisonParty != null)
                DestroyPartyAction.Apply(null, fixture.Settlement.Town.GarrisonParty);
            return;
        }

        var currentGarrison = fixture.Settlement.Town.GarrisonParty;
        if (currentGarrison == null)
            fixture.Settlement.AddGarrisonParty();

        if (fixture.Settlement.Town.GarrisonParty == null)
            throw new InvalidOperationException("Unable to recreate the settlement garrison.");
    }

    private static void RestorePrisonerPromptMilitia(PrisonerPromptFixture fixture)
    {
        var snapshot = fixture.PartySnapshots.SingleOrDefault(party => party.IsSettlementMilitia);
        var currentMilitia = fixture.Settlement.MilitiaPartyComponent?.MobileParty;
        if (snapshot == null)
        {
            if (currentMilitia != null)
                DestroyPartyAction.Apply(null, currentMilitia);
            return;
        }

        if (currentMilitia == null)
        {
            MilitiaPartyComponent.CreateMilitiaParty(
                "militias_of_" + fixture.Settlement.StringId + "_aaa1",
                fixture.Settlement);
        }

        if (fixture.Settlement.MilitiaPartyComponent?.MobileParty == null)
            throw new InvalidOperationException("Unable to recreate the settlement militia.");
    }

    private static PartyBase ResolvePrisonerPromptParty(
        PrisonerPromptFixture fixture,
        PrisonerPromptPartySnapshot snapshot)
    {
        if (snapshot.IsSettlementGarrison)
            return fixture.Settlement.Town.GarrisonParty?.Party;
        if (snapshot.IsSettlementMilitia)
            return fixture.Settlement.MilitiaPartyComponent?.MobileParty?.Party;
        return snapshot.Party;
    }

    private static PartyBase ResolvePrisonerPromptParty(
        PrisonerPromptFixture fixture,
        PartyBase originalParty)
    {
        var snapshot = fixture.PartySnapshots.FirstOrDefault(party => party.Party == originalParty);
        return snapshot == null ? originalParty : ResolvePrisonerPromptParty(fixture, snapshot);
    }

    private static void RestorePrisonerPromptParty(
        PrisonerPromptFixture fixture,
        PrisonerPromptPartySnapshot snapshot)
    {
        var party = ResolvePrisonerPromptParty(fixture, snapshot);
        if (party == null)
            throw new InvalidOperationException($"Unable to resolve restored party {snapshot.StringId}.");

        RestorePrisonerPromptRoster(party.MemberRoster, snapshot.MemberRoster);
        RestorePrisonerPromptRoster(party.PrisonRoster, snapshot.PrisonRoster);
        party.ItemRoster.Clear();
        foreach (var item in snapshot.Items)
            party.ItemRoster.AddToCounts(item.EquipmentElement, item.Amount);

        var mobileParty = party.MobileParty;
        if (mobileParty == null)
            return;

        if (!snapshot.IsSettlementGarrison && !snapshot.IsSettlementMilitia)
            mobileParty.IsActive = snapshot.WasActive;
        mobileParty.RecentEventsMorale = snapshot.RecentEventsMorale;
        mobileParty.PartyTradeGold = snapshot.PartyTradeGold;
        mobileParty.Position = snapshot.Position;
        mobileParty.ChangePartyLeader(snapshot.LeaderHero);
    }

    private static void RestorePrisonerPromptRoster(
        TroopRoster roster,
        TroopRosterElement[] baseline)
    {
        for (int index = roster.Count - 1; index >= 0; index--)
        {
            var element = roster.GetElementCopyAtIndex(index);
            roster.AddToCountsAtIndex(
                index,
                -element.Number,
                -element.WoundedNumber,
                0,
                false);
        }
        roster.RemoveZeroCounts();

        foreach (var element in baseline)
        {
            roster.AddToCounts(
                element.Character,
                element.Number,
                false,
                element.WoundedNumber,
                element.Xp,
                true);
        }
    }

    private static void RestorePrisonerPromptHeroMembership(
        PrisonerPromptFixture fixture,
        PrisonerPromptHeroSnapshot snapshot)
    {
        var prisonerParty = ResolvePrisonerPromptParty(fixture, snapshot.PrisonerParty);
        var party = ResolvePrisonerPromptParty(fixture, snapshot.Party?.Party)?.MobileParty;
        if (snapshot.Hero.PartyBelongedToAsPrisoner != prisonerParty)
        {
            if (snapshot.Hero.PartyBelongedToAsPrisoner != null)
                snapshot.Hero.OnRemovedFromPartyAsPrisoner(snapshot.Hero.PartyBelongedToAsPrisoner);
            if (prisonerParty != null)
                snapshot.Hero.OnAddedToPartyAsPrisoner(prisonerParty);
        }

        if (snapshot.Hero.PartyBelongedTo != party)
        {
            if (snapshot.Hero.PartyBelongedTo != null)
                snapshot.Hero.OnRemovedFromParty(snapshot.Hero.PartyBelongedTo);
            if (party != null)
                snapshot.Hero.OnAddedToParty(party);
        }
    }

    private static void RestorePrisonerPromptClan(PrisonerPromptClanSnapshot snapshot)
    {
        snapshot.Clan._influence = snapshot.Influence;
        if (snapshot.Clan.Renown != snapshot.Renown)
            snapshot.Clan.AddRenown(snapshot.Renown - snapshot.Clan.Renown);
        snapshot.Clan._tier = snapshot.Tier;
    }

    private static bool IsPrisonerPromptCombatStateRestored(PrisonerPromptFixture fixture)
    {
        bool townRestored = fixture.Settlement.Town.Prosperity == fixture.Prosperity &&
            fixture.Settlement.Town.Loyalty == fixture.Loyalty &&
            fixture.Settlement.Town.Security == fixture.Security &&
            fixture.Settlement.Town.FoodStocks == fixture.FoodStocks &&
            fixture.Settlement.Town.Governor == fixture.Governor &&
            fixture.Settlement.Town.LastCapturedBy == fixture.LastCapturedBy &&
            (fixture.HadGarrison
                ? fixture.Settlement.Town.GarrisonParty?.IsActive == true
                : fixture.Settlement.Town.GarrisonParty == null) &&
            (fixture.PartySnapshots.Any(snapshot => snapshot.IsSettlementMilitia)
                ? fixture.Settlement.MilitiaPartyComponent?.MobileParty?.IsActive == true
                : fixture.Settlement.MilitiaPartyComponent == null);
        return townRestored &&
            fixture.PartySnapshots.All(snapshot => IsPrisonerPromptPartyRestored(fixture, snapshot)) &&
            fixture.HeroSnapshots.All(snapshot => IsPrisonerPromptHeroRestored(fixture, snapshot)) &&
            fixture.ClanSnapshots.All(snapshot =>
                snapshot.Clan._influence == snapshot.Influence &&
                snapshot.Clan.Renown == snapshot.Renown &&
                snapshot.Clan._tier == snapshot.Tier);
    }

    private static bool IsPrisonerPromptPartyRestored(
        PrisonerPromptFixture fixture,
        PrisonerPromptPartySnapshot snapshot)
    {
        var party = ResolvePrisonerPromptParty(fixture, snapshot);
        if (party == null ||
            !IsPrisonerPromptRosterRestored(party.MemberRoster, snapshot.MemberRoster) ||
            !IsPrisonerPromptRosterRestored(party.PrisonRoster, snapshot.PrisonRoster))
            return false;

        var items = party.ItemRoster.ToArray();
        if (items.Length != snapshot.Items.Length || snapshot.Items.Any(expected =>
            !items.Any(actual => actual.EquipmentElement.Equals(expected.EquipmentElement) &&
                actual.Amount == expected.Amount)))
            return false;

        var mobileParty = party.MobileParty;
        return mobileParty == null ||
            (((snapshot.IsSettlementGarrison || snapshot.IsSettlementMilitia)
                ? mobileParty.IsActive
                : mobileParty.IsActive == snapshot.WasActive) &&
             mobileParty.RecentEventsMorale == snapshot.RecentEventsMorale &&
             mobileParty.PartyTradeGold == snapshot.PartyTradeGold &&
             mobileParty.Position == snapshot.Position &&
             mobileParty.LeaderHero == snapshot.LeaderHero);
    }

    private static bool IsPrisonerPromptRosterRestored(
        TroopRoster roster,
        TroopRosterElement[] baseline)
    {
        var current = roster.GetTroopRoster().ToArray();
        return current.Length == baseline.Length && baseline.All(expected =>
            current.Any(actual => actual.Character == expected.Character &&
                actual.Number == expected.Number &&
                actual.WoundedNumber == expected.WoundedNumber &&
                actual.Xp == expected.Xp));
    }

    private static bool IsPrisonerPromptHeroRestored(
        PrisonerPromptFixture fixture,
        PrisonerPromptHeroSnapshot snapshot)
    {
        var party = ResolvePrisonerPromptParty(fixture, snapshot.Party?.Party)?.MobileParty;
        var prisonerParty = ResolvePrisonerPromptParty(fixture, snapshot.PrisonerParty);
        bool progressionRestored = snapshot.Hero.HeroState == snapshot.State &&
            snapshot.Hero.PartyBelongedTo == party &&
            snapshot.Hero.PartyBelongedToAsPrisoner == prisonerParty &&
            snapshot.Hero.HitPoints == snapshot.HitPoints &&
            snapshot.Hero.Gold == snapshot.Gold &&
            snapshot.Hero.DeathMark == snapshot.DeathMark &&
            snapshot.Hero.DeathMarkKillerHero == snapshot.DeathMarkKillerHero &&
            snapshot.SkillLevels.All(skill => snapshot.Hero.GetSkillValue(skill.Key) == skill.Value);
        if (!progressionRestored || snapshot.SkillXps == null)
            return progressionRestored;

        return snapshot.Hero.HeroDeveloper != null &&
            snapshot.SkillXps.All(skill => snapshot.Hero.HeroDeveloper.GetSkillXp(skill.Key) == skill.Value) &&
            snapshot.Hero.HeroDeveloper._totalXp == snapshot.TotalXp &&
            snapshot.Hero.HeroDeveloper.UnspentFocusPoints == snapshot.UnspentFocusPoints &&
            snapshot.Hero.HeroDeveloper.UnspentAttributePoints == snapshot.UnspentAttributePoints;
    }

    private static void PublishPrisonerPromptForcedPosition(MobileParty party)
    {
        MessageBroker.Instance.Publish(
            typeof(SiegeDebugCommand),
            new PartyBehaviorChangeAttempted(
                party,
                forcePosition: true,
                isCurrentlyAtSea: party.IsCurrentlyAtSea));
    }

    internal static bool IsPrisonerPromptFixtureHero(Hero hero) =>
        prisonerPromptFixture?.HeroSnapshots.Any(snapshot => snapshot.Hero == hero) == true;

    /// <summary>
    /// Creates a player-led siege and sends a multi-party defending army to interrupt it. Server only.
    /// </summary>
    [CommandLineArgumentFunction("start_army_relief", "coop.debug.siege")]
    public static string StartArmyRelief(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (args.Count < 2 || args.Count > 3)
        {
            return "Usage: coop.debug.siege.start_army_relief <controllerId> <settlementId> [armyPartyCount]";
        }

        int armyPartyCount = 3;
        if (args.Count == 3 && (!int.TryParse(args[2], out armyPartyCount) || armyPartyCount < 2))
        {
            return "armyPartyCount must be at least 2";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)
            || !ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
        {
            return "Unable to resolve siege test services";
        }

        if (!playerManager.TryGetPlayer(args[0], out var player) || !playerManager.IsConnected(player))
        {
            return $"No connected player has controller id {args[0]}";
        }

        if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
        {
            return $"Unable to resolve player party {player.MobilePartyId}";
        }

        if (!objectManager.TryGetObject<Settlement>(args[1], out var settlement))
        {
            return $"Settlement with id {args[1]} not found";
        }

        if (!settlement.IsFortification || settlement.MapFaction is not Kingdom kingdom)
        {
            return $"{settlement.Name} must be a kingdom fortification";
        }

        if (settlement.SiegeEvent != null || playerParty.MapEvent != null || playerParty.BesiegerCamp != null)
        {
            return $"{settlement.Name} or {playerParty.Name} is already in a siege or battle";
        }

        if (playerParty.MapFaction == null)
        {
            return $"{playerParty.Name} has no map faction";
        }

        if (!playerParty.MapFaction.IsAtWarWith(settlement.MapFaction))
        {
            DeclareWarAction.ApplyByDefault(playerParty.MapFaction, settlement.MapFaction);
        }

        var defenders = MobileParty.AllLordParties
            .Where(party => party.IsActive && !party.IsPlayerParty()
                && party.MapFaction == settlement.MapFaction && party.LeaderHero != null
                && party.MapEvent == null && party.CurrentSettlement == null
                && party.BesiegerCamp == null && party.Army == null
                && party.MemberRoster.TotalHealthyCount > 0)
            .OrderByDescending(party => party.Party.CalculateCurrentStrength())
            .Take(armyPartyCount)
            .ToList();

        if (defenders.Count < armyPartyCount)
        {
            return $"Only found {defenders.Count} available {settlement.MapFaction.Name} lord parties; need {armyPartyCount}";
        }

        siegeEventInterface.StartSiegeEvent(playerParty, settlement);
        foreach (var otherPlayer in playerManager.Players)
        {
            if (otherPlayer.ControllerId == player.ControllerId || !playerManager.IsConnected(otherPlayer)) continue;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(otherPlayer.MobilePartyId, out var otherParty)) continue;
            if (otherParty.MapEvent != null || otherParty.BesiegerCamp != null) continue;
            if (settlement.SiegeEvent?.CanPartyJoinSide(otherParty.Party, BattleSideEnum.Attacker) != true) continue;

            siegeEventInterface.JoinSiegeCamp(otherParty, settlement);
        }

        var armyLeader = defenders[0];
        kingdom.CreateArmy(armyLeader.LeaderHero, settlement, ArmyTypes.Defender);
        var army = armyLeader.Army;
        if (army == null)
        {
            return $"Failed to create a relief army led by {armyLeader.Name}";
        }

        armyLeader.Position = playerParty.Position;
        foreach (var defender in defenders.Skip(1))
        {
            defender.Position = playerParty.Position;
            defender.Army = army;
            army.AddPartyToMergedParties(defender);
        }

        StartBattleAction.Apply(armyLeader.Party, playerParty.Party);

        return $"Started {settlement.Name} siege relief: {army.Name} with {army.Parties.Count} parties is attacking " +
            $"{playerParty.Name}; connected friendly player parties joined the siege";
    }

    [CommandLineArgumentFunction("army_relief_state", "coop.debug.siege")]
    public static string ArmyReliefState(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.siege.army_relief_state <controllerId> <settlementId>";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)
            || !playerManager.TryGetPlayer(args[0], out var player)
            || !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty)
            || !objectManager.TryGetObject<Settlement>(args[1], out var settlement))
        {
            return "Unable to resolve the relief fixture";
        }

        bool siegeActive = settlement.SiegeEvent != null;
        bool playerBesieger = siegeActive && playerParty.BesiegerCamp == settlement.SiegeEvent.BesiegerCamp;
        var mapEvent = playerParty.MapEvent;
        var reliefArmy = mapEvent?.InvolvedParties
            .Select(party => party.MobileParty?.Army)
            .FirstOrDefault(army => army?.LeaderParty.MapFaction == settlement.MapFaction);
        int involvedReliefParties = reliefArmy == null
            ? 0
            : mapEvent.InvolvedParties.Count(party => party.MobileParty?.Army == reliefArmy);
        bool reliefEncounterActive = mapEvent != null && reliefArmy != null;
        return $"siege={siegeActive} playerBesieger={playerBesieger} " +
            $"reliefArmyParties={involvedReliefParties} reliefArmyMembers={reliefArmy?.Parties.Count ?? 0} " +
            $"reliefEncounter={reliefEncounterActive} " +
            $"playerMapEvent={mapEvent != null}";
    }

    [CommandLineArgumentFunction("request_besiege", "coop.debug.siege")]
    public static string RequestBesiege(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.request_besiege <settlementId>";
        }

        if (ModInformation.IsServer)
        {
            return "This command can only be used by a client";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        MessageBroker.Instance.Publish(null, new BesiegeSettlementAttempted(MobileParty.MainParty, settlement));
        return $"Requested that the local player party besiege {settlement.Name}";
    }

    [CommandLineArgumentFunction("request_assault", "coop.debug.siege")]
    public static string RequestAssault(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.request_assault <settlementId>";
        }

        if (ModInformation.IsServer)
        {
            return "This command can only be used by a client";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        MessageBroker.Instance.Publish(null, new AssaultSiegeAttempted(MobileParty.MainParty, settlement));
        return $"Requested that the local player party assault {settlement.Name}";
    }

    [CommandLineArgumentFunction("join_active_assault", "coop.debug.siege")]
    public static string JoinActiveAssault(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.join_active_assault <settlementId>";
        }

        if (ModInformation.IsServer)
        {
            return "This command can only be used by a client";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<ISettlementInterface>(out var settlementInterface)
            || !objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Unable to resolve the settlement encounter for {args[0]}";
        }

        var mapEvent = settlement.Party.MapEvent;
        if (mapEvent?.IsSiegeAssault != true)
        {
            return $"{settlement.Name} does not have an active siege assault";
        }

        if (!mapEvent.CanPartyJoinBattle(PartyBase.MainParty, BattleSideEnum.Attacker))
        {
            return $"The local player party cannot join the assault at {settlement.Name}";
        }

        settlementInterface.StartSettlementEncounter(MobileParty.MainParty, settlement);
        if (PlayerEncounter.Current == null)
        {
            return $"Unable to start the local encounter at {settlement.Name}";
        }

        PlayerEncounter.JoinBattle(BattleSideEnum.Attacker);
        GameMenu.SwitchToMenu("menu_siege_strategies");
        MobileParty.MainParty.SetMoveModeHold();
        return $"Joined the active siege assault at {settlement.Name}";
    }

    [CommandLineArgumentFunction("assault_entry_state", "coop.debug.siege")]
    public static string AssaultEntryState(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.siege.assault_entry_state";
        }

        if (ModInformation.IsServer)
        {
            return "This command can only be used by a client";
        }

        if (Campaign.Current == null || PlayerEncounter.Current == null || MobileParty.MainParty == null)
        {
            return "The local player encounter is unavailable";
        }

        var callbackArgs = new MenuCallbackArgs((MenuContext)null, null);
        bool conditionShown = new EncounterGameMenuBehavior()
            .game_menu_encounter_attack_on_condition(callbackArgs);
        var menu = Campaign.Current?.CurrentMenuContext?.GameMenu;
        var renderedOption = menu?.MenuOptions
            .FirstOrDefault(option => option.IdString == "attack");
        var settlement = MobileParty.MainParty?.BesiegedSettlement;
        var leader = settlement?.SiegeEvent?.BesiegerCamp?.LeaderParty;

        return $"menu={menu?.StringId ?? "none"} settlement={settlement?.StringId ?? "none"} " +
            $"leader={leader?.StringId ?? "none"} localLeader={leader == MobileParty.MainParty} " +
            $"conditionShown={conditionShown} conditionEnabled={callbackArgs.IsEnabled} " +
            $"conditionTooltip={callbackArgs.Tooltip?.ToString() ?? "none"} " +
            $"renderedRegistered={renderedOption != null} renderedEnabled={renderedOption?.IsEnabled ?? false} " +
            $"renderedTooltip={renderedOption?.Tooltip?.ToString() ?? "none"}";
    }

    [CommandLineArgumentFunction("leave", "coop.debug.siege")]
    public static string Leave(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.siege.leave";
        }

        if (ModInformation.IsServer)
        {
            return "This command can only be used by a client";
        }

        if (MobileParty.MainParty == null)
        {
            return "The local player party is unavailable";
        }

        MessageBroker.Instance.Publish(null, new BreakSiegeAttempted(MobileParty.MainParty));
        return "Requested that the local player party leave its siege";
    }

    [CommandLineArgumentFunction("leave_settlement", "coop.debug.siege")]
    public static string LeaveSettlement(List<string> args)
    {
        if (args.Count != 0)
        {
            return "Usage: coop.debug.siege.leave_settlement";
        }

        if (ModInformation.IsServer)
        {
            return "This command can only be used by a client";
        }

        var party = MobileParty.MainParty;
        if (party == null)
        {
            return "The local player party is unavailable";
        }

        if (party.CurrentSettlement == null)
        {
            return "The local player party is not in a settlement encounter";
        }

        var settlementName = party.CurrentSettlement.Name;
        PlayerLeaveSettlementPatch.RequestLeave();
        return $"Requested that the local player party leave {settlementName}";
    }

    // coop.debug.siege.start
    /// <summary>
    /// Starts a siege of a settlement, led by the given party or the strongest hostile lord party when
    /// none is given. Server only; the siege replicates to clients.
    /// </summary>
    /// <param name="args">first arg : settlementId ; optional second arg : besieger partyId</param>
    /// <returns>Result of the operation as a string</returns>
    [CommandLineArgumentFunction("start", "coop.debug.siege")]
    public static string StartSiege(List<string> args)
    {
        if (args.Count < 1 || args.Count > 2)
        {
            return "Usage: coop.debug.siege.start <settlementId> [besiegerPartyId]";
        }

        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        if (!settlement.IsFortification)
        {
            return $"{settlement.Name} is not a fortification";
        }

        if (settlement.SiegeEvent != null)
        {
            return $"{settlement.Name} is already under siege";
        }

        MobileParty besieger;
        if (args.Count == 2)
        {
            if (!objectManager.TryGetObject(args[1], out besieger))
            {
                return $"Party with id {args[1]} not found";
            }
        }
        else
        {
            var connectedPlayerFactions = new List<IFaction>();
            if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            {
                return "Unable to resolve PlayerManager";
            }

            foreach (var player in playerManager.Players.Where(playerManager.IsConnected))
            {
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var playerParty))
                {
                    return $"Unable to resolve player party {player.MobilePartyId}";
                }

                if (playerParty.MapFaction == null)
                {
                    return $"Player party {player.MobilePartyId} has no map faction";
                }

                connectedPlayerFactions.Add(playerParty.MapFaction);
            }

            besieger = MobileParty.AllLordParties
                .Where(party => !party.IsPlayerParty()
                    && party.MapFaction?.IsAtWarWith(settlement.MapFaction) == true
                    && connectedPlayerFactions.All(playerFaction =>
                        !party.MapFaction.IsAtWarWith(playerFaction))
                    && party.LeaderHero != null && party.CurrentSettlement == null
                    && party.MapEvent == null && party.BesiegerCamp == null && party.Army == null)
                .OrderByDescending(party => party.Party.CalculateCurrentStrength())
                .FirstOrDefault();
            if (besieger == null)
            {
                return $"No hostile lord party compatible with the connected players is available to besiege " +
                    $"{settlement.Name}; pass a partyId explicitly";
            }
        }

        var originalPosition = besieger.Position;

        // Put the besieger at the gate and commit its AI to the siege.
        besieger.Position = settlement.GatePosition;
        besieger.SetMoveBesiegeSettlement(settlement, MobileParty.NavigationType.Default);
        Campaign.Current.SiegeEventManager.StartSiegeEvent(settlement, besieger);

        string structuredResult = JsonConvert.SerializeObject(new
        {
            settlementId = settlement.StringId,
            besiegerPartyId = besieger.StringId,
            originalX = originalPosition.X,
            originalY = originalPosition.Y,
            originalIsOnLand = originalPosition.IsOnLand,
        });
        return $"{besieger.Name} ({besieger.StringId}) is now besieging {settlement.Name}\n" +
            $"Restore with: coop.debug.siege.stop {settlement.StringId} " +
            $"{originalPosition.X:R} {originalPosition.Y:R} {originalPosition.IsOnLand}\n" +
            "LIVE_TEST_JSON=" + structuredResult;
    }

    /// <summary>
    /// Ends an AI-led siege through the normal authoritative leave path. Server only.
    /// </summary>
    [CommandLineArgumentFunction("stop", "coop.debug.siege")]
    public static string StopSiege(List<string> args)
    {
        if (args.Count != 4)
        {
            return "Usage: coop.debug.siege.stop <settlementId> <originalX> <originalY> <originalIsOnLand>";
        }

        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        var camp = settlement.SiegeEvent?.BesiegerCamp;
        var leader = camp?.LeaderParty;
        if (leader == null)
        {
            return $"{settlement.Name} has no active siege leader";
        }

        if (!ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
        {
            return "Unable to resolve SiegeEventInterface";
        }

        var siegeParties = camp._besiegerParties.ToArray();
        foreach (var party in siegeParties)
        {
            if (party != leader)
            {
                siegeEventInterface.BreakSiege(party);
            }
        }

        siegeEventInterface.BreakSiege(leader);
        if (settlement.SiegeEvent != null)
        {
            return $"Failed to stop the siege of {settlement.Name}; " +
                $"{settlement.SiegeEvent.BesiegerCamp?._besiegerParties.Count ?? 0} parties remain";
        }

        var restoreResult = PartyCommands.RestorePositionCommand(new List<string>
        {
            leader.StringId,
            args[1],
            args[2],
            args[3],
        });
        return $"Stopped the siege of {settlement.Name} led by {leader.Name} ({leader.StringId})\n" +
            restoreResult;
    }

    /// <summary>
    /// Joins every connected player party to an active siege on the authoritative server.
    /// </summary>
    [CommandLineArgumentFunction("join_players", "coop.debug.siege")]
    public static string JoinPlayers(List<string> args)
    {
        if (args.Count != 2 || !int.TryParse(args[1], out int expectedPlayerCount) || expectedPlayerCount < 1)
        {
            return "Usage: coop.debug.siege.join_players <settlementId> <expectedPlayerCount>";
        }

        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)
            || !ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
        {
            return "Unable to resolve siege fixture services";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        var camp = settlement.SiegeEvent?.BesiegerCamp;
        if (camp == null)
        {
            return $"{settlement.Name} is not under siege";
        }

        var connectedPlayers = playerManager.Players.Where(playerManager.IsConnected).ToArray();
        if (connectedPlayers.Length != expectedPlayerCount)
        {
            return $"Expected {expectedPlayerCount} connected players, found {connectedPlayers.Length}";
        }

        var parties = new List<(string ControllerId, string PartyId, MobileParty Party)>();
        foreach (var player in connectedPlayers)
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party))
            {
                return $"Unable to resolve player party {player.MobilePartyId}";
            }

            if (!party.IsActive || party.MapEvent != null || party.BesiegerCamp != null || party.CurrentSettlement != null)
            {
                return $"Player {player.ControllerId} is not clean for the fixture: " +
                    $"active={party.IsActive} mapEvent={party.MapEvent != null} " +
                    $"besiegerCamp={party.BesiegerCamp != null} settlement={party.CurrentSettlement?.StringId ?? "none"}";
            }

            if (!settlement.SiegeEvent.CanPartyJoinSide(party.Party, BattleSideEnum.Attacker))
            {
                return $"Player {player.ControllerId} cannot join the attacking side at {settlement.Name}";
            }

            parties.Add((player.ControllerId, player.MobilePartyId, party));
        }

        for (int i = 0; i < parties.Count; i++)
        {
            for (int j = i + 1; j < parties.Count; j++)
            {
                if (parties[i].Party.MapFaction.IsAtWarWith(parties[j].Party.MapFaction))
                {
                    return $"Players {parties[i].ControllerId} and {parties[j].ControllerId} cannot join the same siege side";
                }
            }
        }

        var joined = new List<string>();
        foreach (var item in parties)
        {
            siegeEventInterface.JoinSiegeCamp(item.Party, settlement);
            if (item.Party.BesiegerCamp != camp)
            {
                return $"Failed to join player {item.ControllerId} to the siege";
            }

            joined.Add($"{item.ControllerId}:{item.PartyId}");
        }

        return $"Joined {joined.Count} connected player parties to the siege of {settlement.Name}:\n" +
            string.Join(Environment.NewLine, joined);
    }

    [CommandLineArgumentFunction("player_state", "coop.debug.siege")]
    public static string PlayerState(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.player_state <partyId>";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject<MobileParty>(args[0], out var party))
        {
            return $"Party with id {args[0]} not found";
        }

        var mapEvent = party.MapEvent;
        var mapEventId = mapEvent != null && objectManager.TryGetId(mapEvent, out string id) ? id : "none";
        var camp = party.BesiegerCamp?.SiegeEvent?.BesiegedSettlement?.StringId ?? "none";
        var army = party.Army?.LeaderParty?.StringId ?? "none";
        var settlement = party.CurrentSettlement?.StringId ?? "none";
        var heroHitPoints = party.LeaderHero?.HitPoints.ToString() ?? "none";
        bool isMainParty = party == MobileParty.MainParty;

        return $"party={party.StringId} mapEvent={mapEventId} siegeAssault={mapEvent?.IsSiegeAssault == true} " +
            $"side={party.Party.Side} besiegerCamp={camp} army={army} settlement={settlement} heroHitPoints={heroHitPoints} " +
            $"playerSiege={isMainParty && PlayerSiege.PlayerSiegeEvent != null} encounter={isMainParty && PlayerEncounter.Current != null}";
    }

    [CommandLineArgumentFunction("prepare_ladders_only", "coop.debug.siege")]
    public static string PrepareLaddersOnly(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.prepare_ladders_only <settlementId>";
        }

        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        var siegeEvent = settlement.SiegeEvent;
        var attackerEngines = siegeEvent?.BesiegerCamp?.SiegeEngines;
        var defenderEngines = settlement.SiegeEngines;
        if (siegeEvent == null || attackerEngines == null || defenderEngines == null)
        {
            return $"{settlement.Name} is not under siege";
        }

        ClearSiegeEngines(attackerEngines);
        ClearSiegeEngines(defenderEngines);
        if (attackerEngines.DeployedSiegeEngines.Count > 0
            || attackerEngines.ReservedSiegeEngines.Count > 0
            || defenderEngines.DeployedSiegeEngines.Count > 0
            || defenderEngines.ReservedSiegeEngines.Count > 0)
        {
            return $"Failed to remove the campaign siege engines from {settlement.Name}";
        }

        var preparations = attackerEngines.SiegePreparations;
        if (!preparations.IsConstructed)
        {
            preparations.SetProgress(1f);
            siegeEvent.CreateSiegeObject(preparations, siegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker));
        }

        return $"Prepared a ladder-only assault at {settlement.Name} ({settlement.StringId}): " +
            $"preparation={preparations.Progress:0.00} attackerEngines=0 defenderEngines=0";
    }

    private static void ClearSiegeEngines(SiegeEnginesContainer siegeEngines)
    {
        for (int i = siegeEngines.DeployedRangedSiegeEngines.Length - 1; i >= 0; i--)
        {
            if (siegeEngines.DeployedRangedSiegeEngines[i] != null)
            {
                siegeEngines.RemoveDeployedSiegeEngine(i, isRanged: true, moveToReserve: false);
            }
        }

        for (int i = siegeEngines.DeployedMeleeSiegeEngines.Length - 1; i >= 0; i--)
        {
            if (siegeEngines.DeployedMeleeSiegeEngines[i] != null)
            {
                siegeEngines.RemoveDeployedSiegeEngine(i, isRanged: false, moveToReserve: false);
            }
        }

        while (siegeEngines.ReservedSiegeEngines.Count > 0)
        {
            var siegeEngine = siegeEngines.ReservedSiegeEngines[0];
            if (!siegeEngines.RemovedSiegeEngineFromReservedSiegeEngines(siegeEngine))
            {
                break;
            }
        }
    }

    [CommandLineArgumentFunction("stage_machines", "coop.debug.siege")]
    public static string StageMachines(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.stage_machines <settlementId>";
        }

        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<ISiegeEventInterface>(out var siegeEventInterface))
        {
            return "Unable to resolve siege fixture services";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        var siegeEvent = settlement.SiegeEvent;
        if (siegeEvent?.BesiegerCamp == null)
        {
            return $"{settlement.Name} is not under siege";
        }

        var attacker = siegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker);
        if (!attacker.SiegeEngines.SiegePreparations.IsConstructed)
        {
            attacker.SiegeEngines.SiegePreparations.SetProgress(1f);
            siegeEvent.CreateSiegeObject(attacker.SiegeEngines.SiegePreparations, attacker);
        }

        var machines = new[]
        {
            (Side: BattleSideEnum.Attacker, Type: DefaultSiegeEngineTypes.Ram, Index: 0),
            (Side: BattleSideEnum.Attacker, Type: DefaultSiegeEngineTypes.Onager, Index: 0),
            (Side: BattleSideEnum.Defender, Type: DefaultSiegeEngineTypes.Ballista, Index: 0),
        };
        var staged = new List<string>();
        foreach (var machine in machines)
        {
            siegeEventInterface.DeploySiegeEngine(siegeEvent, machine.Side, machine.Type, machine.Index);
            var side = siegeEvent.GetSiegeEventSide(machine.Side);
            var slots = machine.Type.IsRanged
                ? side.SiegeEngines.DeployedRangedSiegeEngines
                : side.SiegeEngines.DeployedMeleeSiegeEngines;
            var progress = machine.Index < slots.Length ? slots[machine.Index] : null;
            if (progress?.SiegeEngine != machine.Type)
            {
                return $"Failed to stage {machine.Type.StringId} for {machine.Side}";
            }

            bool needsSiegeObject = !progress.IsConstructed
                || (machine.Type.IsRanged && progress.RangedSiegeEngine == null);
            if (!progress.IsConstructed)
            {
                progress.SetProgress(1f);
            }
            if (progress.IsBeingRedeployed)
            {
                progress.SetRedeploymentProgress(1f);
            }
            if (needsSiegeObject)
            {
                siegeEvent.CreateSiegeObject(progress, side);
            }
            if (!progress.IsActive)
            {
                return $"Failed to activate {machine.Type.StringId} for {machine.Side}";
            }

            staged.Add($"{machine.Side}:{machine.Type.StringId}[{machine.Index}]");
        }

        return $"Staged {staged.Count} constructed siege engines at {settlement.Name}: {string.Join(", ", staged)}";
    }

    /// <summary>
    /// Starts the wall assault for an existing AI-led siege. Server only; the resulting map event uses the
    /// same authoritative action as campaign AI.
    /// </summary>
    [CommandLineArgumentFunction("assault", "coop.debug.siege")]
    public static string StartAssault(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.assault <settlementId>";
        }

        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        var attacker = settlement.SiegeEvent?.BesiegerCamp?.LeaderParty;
        if (attacker == null)
        {
            return $"{settlement.Name} has no active siege leader";
        }

        if (attacker.IsPlayerParty())
        {
            return $"{settlement.Name} is player-led; this command only starts AI assaults";
        }

        if (attacker.MapEvent != null)
        {
            return $"{attacker.Name} is already in an active map event";
        }

        if (settlement.Party.MapEvent != null)
        {
            return $"{settlement.Name} already has an active map event";
        }

        StartBattleAction.ApplyStartAssaultAgainstWalls(attacker, settlement);

        var mapEvent = settlement.Party.MapEvent;
        if (mapEvent?.IsSiegeAssault != true)
        {
            return $"Failed to start an AI siege assault against {settlement.Name}";
        }

        var mapEventId = objectManager.TryGetId(mapEvent, out string id) ? id : mapEvent.StringId;
        return $"Started AI siege assault by {attacker.Name} against {settlement.Name} (MapEvent {mapEventId})" +
            Environment.NewLine + "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
            {
                settlementId = settlement.StringId,
                mapEventId,
            });
    }

    /// <summary>
    /// Reports the exact vanilla readiness inputs and the starvation terminal decision. Read-only.
    /// </summary>
    [CommandLineArgumentFunction("terminal_status", "coop.debug.siege")]
    public static string TerminalStatus(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.terminal_status town_ES1";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<IAiSiegeAssaultReadiness>(out var readiness)
            || !ContainerProvider.TryResolve<IAiSiegeTerminalPolicy>(out var terminalPolicy))
        {
            return "Unable to resolve AI siege terminal services";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        var siegeEvent = settlement.SiegeEvent;
        var camp = siegeEvent?.BesiegerCamp;
        var leader = camp?.LeaderParty;
        if (siegeEvent == null || camp == null || leader == null)
        {
            return $"{settlement.Name} has no active siege leader";
        }

        var result = readiness.Evaluate(camp);
        int starvingParties = 0;
        int commandGroupParties = 0;
        if (leader.Army?.LeaderParty == leader)
        {
            commandGroupParties = leader.Army.LeaderPartyAndAttachedPartiesCount;
            starvingParties = leader.Party.IsStarving ? 1 : 0;
            starvingParties += leader.AttachedParties.Count(party => party.Party.IsStarving);
        }

        bool foodProblem = commandGroupParties > 0
            && (float)starvingParties / (float)commandGroupParties > 0.5f;
        bool activeTransition = leader.MapEvent != null || settlement.Party.MapEvent != null;
        var decision = terminalPolicy.GetDecision(new AiSiegeTerminalContext(
            foodProblem,
            camp.IsPreparationComplete,
            leader.IsPlayerParty(),
            isCurrentSiege: true,
            activeTransition,
            result.IsViable));

        return $"settlement={settlement.StringId} leader={leader.StringId} playerLed={leader.IsPlayerParty()} " +
            $"prepared={camp.IsPreparationComplete} elapsedHours={siegeEvent.SiegeStartTime.ElapsedHoursUntilNow:0.0} " +
            $"starving={starvingParties}/{commandGroupParties} foodProblem={foodProblem} activeTransition={activeTransition} " +
            $"attacker={result.AttackerStrength:0.00} defender={result.DefenderStrength:0.00} " +
            $"powerRatio={result.PowerRatioBeforeEquipment:0.000} adjustedRatio={result.PowerRatioAfterEquipment:0.000} " +
            $"assaultChance={result.AssaultChance:0.000} viable={result.IsViable} decision={decision}";
    }

    /// <summary>
    /// Simulates the vanilla food-problem terminal event for an AI siege. Server only.
    /// </summary>
    [CommandLineArgumentFunction("resolve_starvation", "coop.debug.siege")]
    public static string ResolveStarvation(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.resolve_starvation town_ES1";
        }

        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)
            || !ContainerProvider.TryResolve<IAiSiegeTerminalPolicy>(out var terminalPolicy))
        {
            return "Unable to resolve AI siege terminal services";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        var siegeEvent = settlement.SiegeEvent;
        var leader = siegeEvent?.BesiegerCamp?.LeaderParty;
        if (siegeEvent == null || leader == null)
        {
            return $"{settlement.Name} has no active siege leader";
        }

        var decision = terminalPolicy.ResolveFoodProblem(
            new AiSiegeTerminalTransitionState(leader, siegeEvent));
        return $"Simulated starvation terminal policy at {settlement.Name} ({settlement.StringId}): {decision}";
    }

    // coop.debug.siege.list
    /// <summary>
    /// Lists the active sieges with their preparation progress and deployed engine counts.
    /// </summary>
    /// <param name="args">no args</param>
    /// <returns>Result of the operation as a string</returns>
    [CommandLineArgumentFunction("list", "coop.debug.siege")]
    public static string ListSieges(List<string> args)
    {
        var siegeEvents = SiegeContainerLookup.ActiveSieges().ToList();
        if (siegeEvents.Count == 0)
        {
            return "No active sieges";
        }

        var sb = new StringBuilder();
        foreach (var siegeEvent in siegeEvents)
        {
            var camp = siegeEvent.BesiegerCamp;
            sb.AppendLine($"{siegeEvent.BesiegedSettlement?.Name} ({siegeEvent.BesiegedSettlement?.StringId}): " +
                $"leader={camp?.LeaderParty?.Name} preparation={camp?.SiegeEngines?.SiegePreparations?.Progress:0.00} " +
                $"attackerEngines={camp?.SiegeEngines?.DeployedSiegeEngines?.Count ?? 0} " +
                $"defenderEngines={siegeEvent.BesiegedSettlement?.SiegeEngines?.DeployedSiegeEngines?.Count ?? 0} " +
                $"strategy={camp?.SiegeStrategy?.Name}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Reports whether a settlement's replicated siege graph is ready for map visuals. Read-only.
    /// </summary>
    [CommandLineArgumentFunction("graph", "coop.debug.siege")]
    public static string GraphState(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.graph <settlementId>";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        var siegeEvent = settlement.SiegeEvent;
        if (siegeEvent == null)
        {
            return $"{settlement.Name} ({settlement.StringId}): siege=False graphComplete=False";
        }

        var camp = siegeEvent.BesiegerCamp;
        return $"{settlement.Name} ({settlement.StringId}): siege=True " +
            $"camp={camp != null} leader={camp?.LeaderParty != null} " +
            $"attackerContainer={camp?.SiegeEngines != null} " +
            $"defenderContainer={settlement.SiegeEngines != null} " +
            $"graphComplete={SiegeContainerLookup.IsGraphComplete(siegeEvent)}";
    }

    /// <summary>
    /// Centers the client campaign camera on a settlement for visual inspection.
    /// </summary>
    [CommandLineArgumentFunction("focus", "coop.debug.siege")]
    public static string FocusSettlement(List<string> args)
    {
        if (ModInformation.IsServer)
        {
            return "This command can only be used by a client";
        }

        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.focus <settlementId>";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        if (MapScreen.Instance == null)
        {
            return "The campaign map screen is not active";
        }

        MapScreen.Instance.FastMoveCameraToPosition(settlement.Position);
        return $"Centered the campaign camera on {settlement.Name} ({settlement.StringId})";
    }

    // coop.debug.siege.dump_party <heroName|main|partyId>
    /// <summary>
    /// Dumps a party's siege-relevant state — CurrentSettlement, BesiegerCamp, BesiegedSettlement, Position —
    /// with its co-op registry id. Read-only; run on the SERVER and BOTH clients right after a siege capture and
    /// compare the co-besieger's party. A CurrentSettlement set on the server/host but null on the co-besieger's
    /// own client (party still at the camp Position, its BesiegerCamp maybe uncleared) pinpoints why it is left
    /// outside. Resolve by "main" (that client's own party), a coop id, or a leader-hero name.
    /// </summary>
    /// <param name="args">first arg: heroName | main | partyId</param>
    /// <returns>Result of the operation as a string</returns>
    [CommandLineArgumentFunction("dump_party", "coop.debug.siege")]
    public static string DumpParty(List<string> args)
    {
        if (args.Count != 1)
        {
            return "Usage: coop.debug.siege.dump_party <heroName|main|partyId>";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        string arg = args[0];
        MobileParty party;
        if (arg.Equals("main", StringComparison.OrdinalIgnoreCase))
        {
            party = MobileParty.MainParty;
        }
        else if (!objectManager.TryGetObject(arg, out party))
        {
            party = MobileParty.All.FirstOrDefault(p => p.LeaderHero?.Name != null
                && p.LeaderHero.Name.ToString().IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (party == null)
        {
            return $"No party found for '{arg}' (use \"main\", a coop id, or a leader-hero name)";
        }

        var settlement = party.CurrentSettlement ?? party.BesiegedSettlement;

        var sb = new StringBuilder();
        sb.AppendLine($"[{(ModInformation.IsServer ? "SERVER" : "CLIENT")}] siege state: {party.Name} ({party.StringId}) coopId={IdOf(objectManager, party)}");
        sb.AppendLine($"  Leader: {party.LeaderHero?.Name?.ToString() ?? "null"}  IsActive: {party.IsActive}");
        var pos = party.GetPosition2D;
        sb.AppendLine($"  Position2D: {pos.x:0.00}, {pos.y:0.00}  IsOnLand: {party.Position.IsOnLand}");
        sb.AppendLine($"  CurrentSettlement: {Describe(party.CurrentSettlement)}");
        sb.AppendLine($"  BesiegerCamp: {(party.BesiegerCamp != null ? "present" : "null")}");
        sb.AppendLine($"  BesiegedSettlement: {Describe(party.BesiegedSettlement)}");
        sb.AppendLine($"  MapEvent: {party.MapEvent?.EventType.ToString() ?? "null"}  ShortTermBehavior: {party.ShortTermBehavior}");

        if (settlement != null)
        {
            sb.AppendLine($"  -- {settlement.Name} ({settlement.StringId}): owner={settlement.OwnerClan?.Name?.ToString() ?? "null"} " +
                $"underSiege={settlement.IsUnderSiege} siegeEvent={(settlement.SiegeEvent != null ? "active" : "null")}");
        }

        return sb.ToString();
    }

    // coop.debug.siege.dump_engines
    /// <summary>
    /// Dumps every active siege's engines with their co-op registry id, hitpoints, progress and aim.
    /// Read-only; run on BOTH the server and a client and compare. Matching ids with matching hitpoints
    /// means the sync works (a stale on-screen value is then a UI-refresh bug); a differing or UNREGISTERED
    /// id on the client means it is rendering a local duplicate the server's hitpoint/aim updates never reach.
    /// </summary>
    /// <param name="args">no args</param>
    /// <returns>Result of the operation as a string</returns>
    [CommandLineArgumentFunction("dump_engines", "coop.debug.siege")]
    public static string DumpEngines(List<string> args)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        var siegeEvents = SiegeContainerLookup.ActiveSieges().ToList();
        if (siegeEvents.Count == 0)
        {
            return "No active sieges";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[{(ModInformation.IsServer ? "SERVER" : "CLIENT")}] siege engines:");

        foreach (var siegeEvent in siegeEvents)
        {
            sb.AppendLine($"Siege of {siegeEvent.BesiegedSettlement?.StringId} (SiegeEvent {IdOf(objectManager, siegeEvent)})");
            DumpSide(sb, objectManager, "ATTACKER", siegeEvent.BesiegerCamp?.SiegeEngines);
            DumpSide(sb, objectManager, "DEFENDER", siegeEvent.BesiegedSettlement?.SiegeEngines);
        }

        var dump = sb.ToString();
        // Into the Coop log too, so the machines' dumps can be compared from their log files.
        Logger.Information("[EngineDump]\n{Dump}", dump);
        return dump;
    }

    private static void DumpSide(StringBuilder sb, IObjectManager objectManager, string label, SiegeEnginesContainer container)
    {
        if (container == null)
        {
            sb.AppendLine($"  {label}: no container");
            return;
        }

        DumpEngine(sb, objectManager, $"  {label} prep    ", container.SiegePreparations);
        foreach (var engine in container.DeployedSiegeEngines)
        {
            DumpEngine(sb, objectManager, $"  {label} deployed", engine);
        }
        foreach (var engine in container.ReservedSiegeEngines)
        {
            DumpEngine(sb, objectManager, $"  {label} reserve ", engine);
        }
    }

    private static void DumpEngine(StringBuilder sb, IObjectManager objectManager, string slot, SiegeEngineConstructionProgress engine)
    {
        if (engine == null) return;

        var ranged = engine.RangedSiegeEngine;
        var aim = ranged != null ? $"{ranged.CurrentTargetType}[{ranged.CurrentTargetIndex}]" : "none";
        sb.AppendLine($"{slot}: type={engine.SiegeEngine?.StringId} id={IdOf(objectManager, engine)} " +
            $"hp={engine.Hitpoints:0}/{engine.MaxHitPoints:0} prog={engine.Progress:0.00} redeploy={engine.RedeploymentProgress:0.00} aim={aim}");
    }

    private static string IdOf(IObjectManager objectManager, object obj)
    {
        return obj != null && objectManager.TryGetId(obj, out var id) ? id : "UNREGISTERED";
    }

    private static string Describe(Settlement settlement)
        => settlement != null ? $"{settlement.Name} ({settlement.StringId})" : "null";

    // coop.debug.siege.dump_machines
    /// <summary>
    /// Dumps the siege weapons and deployment points of the current mission — the exact flags the use
    /// prompt and the AI read — sorted so two clients' dumps diff line by line, and written to the Coop
    /// log for post-run comparison. Pass "all" to include every usable machine. Read-only.
    /// </summary>
    [CommandLineArgumentFunction("dump_machines", "coop.debug.siege")]
    public static string DumpMachines(List<string> args)
    {
        var mission = TaleWorlds.MountAndBlade.Mission.Current;
        if (mission == null)
        {
            return "No mission is running";
        }

        bool includeAll = args.Count > 0 && args[0] == "all";
        var lines = new List<string>();
        foreach (var missionObject in mission.MissionObjects)
        {
            if (missionObject is TaleWorlds.MountAndBlade.UsableMachine machine
                && (includeAll || machine is TaleWorlds.MountAndBlade.SiegeWeapon))
            {
                int deactivatedPoints = 0, usedPoints = 0;
                foreach (var point in machine.StandingPoints)
                {
                    if (point.IsDeactivated) deactivatedPoints++;
                    if (point.UserAgent != null) usedPoints++;
                }

                lines.Add($"machine {machine.Id.Id:D5} {machine.GetType().Name,-16}" +
                    $" disabled={(machine.IsDisabled ? 1 : 0)} visible={(machine.GameEntity.IsVisibleIncludeParents() ? 1 : 0)}" +
                    $" deactivated={(machine.IsDeactivated ? 1 : 0)} aiOff={(machine.IsDisabledForAI ? 1 : 0)}" +
                    $" simLocal={(SiegeMissionAuthorityGate.IsMachineSimulatedLocally(machine.Id.Id) ? 1 : 0)}" +
                    $" pts={machine.StandingPoints.Count} ptsOff={deactivatedPoints} ptsUsed={usedPoints}" +
                    DescribeMissionMachineState(machine));
            }
            else if (missionObject is TaleWorlds.MountAndBlade.DeploymentPoint deploymentPoint)
            {
                var variants = deploymentPoint._weapons?
                    .Where(weapon => weapon != null)
                    .ToArray() ?? Array.Empty<TaleWorlds.MountAndBlade.SynchedMissionObject>();
                var deployedWeapon = deploymentPoint.DeployedWeapon;
                var deployedWeaponType = deployedWeapon == null
                    ? "none"
                    : TaleWorlds.MountAndBlade.Missions.MissionSiegeWeaponsController.GetWeaponType(deployedWeapon)?.Name
                        ?? deployedWeapon.GetType().Name;
                lines.Add($"point   {deploymentPoint.Id.Id:D5} {deploymentPoint.Side,-16}" +
                    $" disabled={(deploymentPoint.IsDisabled ? 1 : 0)} deployed={(deploymentPoint.IsDeployed ? 1 : 0)}" +
                    $" weapon={deployedWeaponType}" +
                    $" weaponId={(deployedWeapon != null ? deployedWeapon.Id.Id.ToString("D5") : "none")}" +
                    $" weaponVisible={(deployedWeapon?.GameEntity.IsVisibleIncludeParents() == true ? 1 : 0)}" +
                    $" variants={variants.Length} variantsVisible={variants.Count(weapon => weapon.GameEntity.IsVisibleIncludeParents())}");
            }
        }

        lines.Sort(StringComparer.Ordinal);
        lines.Insert(0, $"siege={mission.IsSiegeBattle} authority={SiegeMissionAuthorityGate.IsLocalAuthority} known={SiegeMissionAuthorityGate.IsAuthorityKnown} entries={lines.Count}");

        var dump = string.Join(Environment.NewLine, lines);
        Logger.Information("[MachineDump]\n{Dump}", dump);
        return dump;
    }

    private static string DescribeMissionMachineState(TaleWorlds.MountAndBlade.UsableMachine machine)
    {
        if (!(machine is TaleWorlds.MountAndBlade.SiegeLadder ladder))
        {
            return string.Empty;
        }

        int animationIndex = ladder._ladderSkeleton.GetAnimationIndexAtChannel(0);
        float animationProgress = animationIndex >= 0
            ? ladder._ladderSkeleton.GetAnimationParameterAtChannel(0)
            : 0f;

        return $" ladderState={ladder.State} ladderAnimation={ladder._animationState}" +
            $" ladderAnimationIndex={animationIndex} ladderProgress={animationProgress:0.000}";
    }
}

[HarmonyPatch(typeof(Hero), nameof(Hero.CanDie))]
internal static class PrisonerPromptFixtureHeroDeathPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        Hero __instance,
        KillCharacterAction.KillCharacterActionDetail causeOfDeath,
        ref bool __result)
    {
        if (causeOfDeath != KillCharacterAction.KillCharacterActionDetail.DiedInBattle ||
            !SiegeDebugCommand.IsPrisonerPromptFixtureHero(__instance))
            return true;

        __result = false;
        return false;
    }
}
