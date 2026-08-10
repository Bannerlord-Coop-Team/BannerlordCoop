#if DEBUG
using Autofac;
using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Party.Commands;

internal static class LargeBattleRosterFixtureCommands
{
    private const string FixtureTroopId = "imperial_recruit";
    private static LargeBattleRosterFixture fixture;

    private sealed class LargeBattleRosterFixture
    {
        public Campaign Campaign;
        public PartySnapshot FirstParty;
        public PartySnapshot SecondParty;
        public int? ExactHealthyCount;
    }

    private sealed class PartySnapshot
    {
        public string PartyId;
        public MobileParty Party;
        public TroopRosterElement[] MemberRoster;
        public string Fingerprint;
        public Hero LeaderHero;
        public Dictionary<Hero, int> HeroHitPoints;
        public bool HasBehavior;
        public PartyBehaviorUpdateData Behavior;
    }

    [CommandLineArgumentFunction("large_battle_roster_begin", "coop.debug.mobileparty")]
    public static string Begin(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 3
            || !int.TryParse(args[2], out int addedPerParty)
            || addedPerParty < 1
            || addedPerParty > 500)
        {
            return "Usage: coop.debug.mobileparty.large_battle_roster_begin " +
                   "<firstPartyId> <secondPartyId> <troopsPerParty:1-500>";
        }
        if (fixture != null)
            return "A large-battle roster fixture is already pending restoration.";
        if (!TryGetObjectManager(out IObjectManager objectManager))
            return "Unable to resolve ObjectManager.";
        if (!TryResolveParty(
                objectManager,
                args[0],
                out MobileParty firstParty,
                out string firstError))
        {
            return firstError;
        }
        if (!TryResolveParty(
                objectManager,
                args[1],
                out MobileParty secondParty,
                out string secondError))
        {
            return secondError;
        }
        if (firstParty == secondParty)
            return "The fixture requires two distinct parties.";
        if (!objectManager.TryGetObject(
                FixtureTroopId,
                out CharacterObject fixtureTroop))
        {
            return $"Unable to resolve fixture troop {FixtureTroopId}.";
        }

        var activeFixture = new LargeBattleRosterFixture
        {
            Campaign = Campaign.Current,
            FirstParty = Capture(firstParty),
            SecondParty = Capture(secondParty),
        };
        fixture = activeFixture;

        firstParty.MemberRoster.AddToCounts(
            fixtureTroop,
            addedPerParty);
        secondParty.MemberRoster.AddToCounts(
            fixtureTroop,
            addedPerParty);

        return
            $"LARGE_BATTLE_ROSTER_FIXTURE_STARTED troop={FixtureTroopId} addedPerParty={addedPerParty}\n" +
            FormatState("active", firstParty, secondParty, null);
    }

    [CommandLineArgumentFunction("exact_battle_roster_begin", "coop.debug.mobileparty")]
    public static string BeginExact(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 3
            || !int.TryParse(args[2], out int healthyPerParty)
            || (healthyPerParty != 5 && healthyPerParty != 900))
        {
            return "Usage: coop.debug.mobileparty.exact_battle_roster_begin " +
                   "<firstPartyId> <secondPartyId> <healthyPerParty:5|900>";
        }
        if (fixture != null)
            return "A large-battle roster fixture is already pending restoration.";
        if (!TryGetObjectManager(out IObjectManager objectManager) ||
            !ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
        {
            return "Unable to resolve the exact battle fixture services.";
        }
        if (!TryResolveParty(
                objectManager,
                args[0],
                out MobileParty firstParty,
                out string firstError))
        {
            return firstError;
        }
        if (!TryResolveParty(
                objectManager,
                args[1],
                out MobileParty secondParty,
                out string secondError))
        {
            return secondError;
        }
        if (firstParty == secondParty)
            return "The fixture requires two distinct parties.";
        if (!CanStageExactBattleParty(firstParty) || !CanStageExactBattleParty(secondParty))
            return "Both parties must be active on the campaign map, outside settlements and map events.";
        if (!objectManager.TryGetObjectWithLogging(
                FixtureTroopId,
                out CharacterObject fixtureTroop))
        {
            return $"Unable to resolve fixture troop {FixtureTroopId}.";
        }
        if (!behaviorSnapshot.TryCreate(firstParty, out var firstBehavior) ||
            !behaviorSnapshot.TryCreate(secondParty, out var secondBehavior))
        {
            return "Unable to capture both parties' original movement state.";
        }

        PartySnapshot firstSnapshot = Capture(firstParty, firstBehavior);
        PartySnapshot secondSnapshot = Capture(secondParty, secondBehavior);
        if (!TryGetFixtureTroopCount(
                firstSnapshot,
                healthyPerParty,
                out int firstTroops,
                out firstError))
        {
            return firstError;
        }
        if (!TryGetFixtureTroopCount(
                secondSnapshot,
                healthyPerParty,
                out int secondTroops,
                out secondError))
        {
            return secondError;
        }

        var activeFixture = new LargeBattleRosterFixture
        {
            Campaign = Campaign.Current,
            FirstParty = firstSnapshot,
            SecondParty = secondSnapshot,
            ExactHealthyCount = healthyPerParty,
        };
        fixture = activeFixture;
        try
        {
            SetExactRoster(firstSnapshot, fixtureTroop, firstTroops);
            SetExactRoster(secondSnapshot, fixtureTroop, secondTroops);
        }
        catch (Exception ex)
        {
            Restore(firstSnapshot);
            Restore(secondSnapshot);
            bool firstBehaviorRestored = RestoreBehavior(firstSnapshot);
            bool secondBehaviorRestored = RestoreBehavior(secondSnapshot);
            bool restored =
                IsPartyStateRestored(firstSnapshot) &&
                IsPartyStateRestored(secondSnapshot) &&
                firstBehaviorRestored &&
                secondBehaviorRestored;
            if (restored) fixture = null;

            return
                $"Unable to stage the exact battle roster fixture: {ex.GetType().Name}: {ex.Message}\n" +
                $"restored={restored}";
        }

        return
            $"EXACT_BATTLE_ROSTER_FIXTURE_STARTED troop={FixtureTroopId} healthyPerParty={healthyPerParty}\n" +
            FormatState("active", firstParty, secondParty, healthyPerParty);
    }

    [CommandLineArgumentFunction("large_battle_roster_status", "coop.debug.mobileparty")]
    public static string Status(List<string> args) =>
        Status(args, "coop.debug.mobileparty.large_battle_roster_status");

    private static string Status(List<string> args, string commandName)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 2)
        {
            return $"Usage: {commandName} <firstPartyId> <secondPartyId>";
        }
        if (!TryGetObjectManager(out IObjectManager objectManager))
            return "Unable to resolve ObjectManager.";
        if (!TryResolveParty(
                objectManager,
                args[0],
                out MobileParty firstParty,
                out string firstError))
        {
            return firstError;
        }
        if (!TryResolveParty(
                objectManager,
                args[1],
                out MobileParty secondParty,
                out string secondError))
        {
            return secondError;
        }

        return FormatState(
            fixture == null ? "none" : "active",
            firstParty,
            secondParty,
            fixture?.ExactHealthyCount);
    }

    [CommandLineArgumentFunction("exact_battle_roster_status", "coop.debug.mobileparty")]
    public static string ExactStatus(List<string> args) =>
        Status(args, "coop.debug.mobileparty.exact_battle_roster_status");

    [CommandLineArgumentFunction("large_battle_roster_restore", "coop.debug.mobileparty")]
    public static string Restore(List<string> args) =>
        Restore(args, "coop.debug.mobileparty.large_battle_roster_restore");

    private static string Restore(List<string> args, string commandName)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 0)
            return $"Usage: {commandName}";
        if (fixture == null)
            return "No large-battle roster fixture is pending restoration.";

        LargeBattleRosterFixture activeFixture = fixture;
        if (activeFixture.Campaign != Campaign.Current)
        {
            fixture = null;
            return "The fixture belongs to a previous campaign and was discarded.";
        }
        if (!TryGetObjectManager(out IObjectManager objectManager))
            return "Unable to resolve ObjectManager.";
        if (!TryResolveSnapshotParty(
                objectManager,
                activeFixture.FirstParty,
                out string firstError))
        {
            fixture = null;
            return firstError;
        }
        if (!TryResolveSnapshotParty(
                objectManager,
                activeFixture.SecondParty,
                out string secondError))
        {
            fixture = null;
            return secondError;
        }

        bool firstRestoredDeadHero = Restore(activeFixture.FirstParty);
        bool secondRestoredDeadHero = Restore(activeFixture.SecondParty);
        bool restoredDeadHero =
            firstRestoredDeadHero || secondRestoredDeadHero;
        bool firstBehaviorRestored = RestoreBehavior(activeFixture.FirstParty);
        bool secondBehaviorRestored = RestoreBehavior(activeFixture.SecondParty);
        bool behaviorRestored = firstBehaviorRestored && secondBehaviorRestored;
        string warning = restoredDeadHero
            ? "WARNING: the restored roster contains a dead hero and may be invalid.\n"
            : string.Empty;

        if (!IsPartyStateRestored(activeFixture.FirstParty)
            || !IsPartyStateRestored(activeFixture.SecondParty)
            || !behaviorRestored)
        {
            return "Large-battle fixture restoration did not restore the original rosters, heroes, leaders, and movement behavior.\n" +
                   warning +
                   FormatState(
                       "restore-failed",
                       activeFixture.FirstParty.Party,
                       activeFixture.SecondParty.Party,
                       activeFixture.ExactHealthyCount);
        }

        fixture = null;
        return
            "LARGE_BATTLE_ROSTER_FIXTURE_RESTORED\n" +
            warning +
            FormatState(
                "none",
                activeFixture.FirstParty.Party,
                activeFixture.SecondParty.Party,
                null);
    }

    [CommandLineArgumentFunction("exact_battle_roster_restore", "coop.debug.mobileparty")]
    public static string RestoreExact(List<string> args) =>
        Restore(args, "coop.debug.mobileparty.exact_battle_roster_restore");

    private static bool TryGetObjectManager(
        out IObjectManager objectManager)
    {
        objectManager = null;
        if (!ContainerProvider.TryGetContainer(
                out ILifetimeScope container))
            return false;

        return container.TryResolve(out objectManager);
    }

    private static bool TryResolveParty(
        IObjectManager objectManager,
        string id,
        out MobileParty party,
        out string error)
    {
        if (objectManager.TryGetObject(id, out party)
            || CommandHelpers.TryGetMobileParty(id, out party, out _))
        {
            error = null;
            return true;
        }

        error = $"Unable to resolve party {id}.";
        return false;
    }

    private static bool TryResolveSnapshotParty(
        IObjectManager objectManager,
        PartySnapshot snapshot,
        out string error)
    {
        if (objectManager.TryGetObjectWithLogging(
                snapshot.PartyId,
                out MobileParty party))
        {
            snapshot.Party = party;
            error = null;
            return true;
        }

        error =
            $"The captured party {snapshot.PartyId} is no longer available; the fixture was discarded.";
        return false;
    }

    private static PartySnapshot Capture(MobileParty party)
    {
        TroopRosterElement[] memberRoster = CopyRoster(party.MemberRoster);
        var heroHitPoints = new Dictionary<Hero, int>();
        foreach (TroopRosterElement element in memberRoster)
        {
            Hero hero = element.Character.HeroObject;
            if (hero != null && !heroHitPoints.ContainsKey(hero))
                heroHitPoints.Add(hero, hero.HitPoints);
        }

        Hero leaderHero = party.LeaderHero;
        if (leaderHero != null && !heroHitPoints.ContainsKey(leaderHero))
            heroHitPoints.Add(leaderHero, leaderHero.HitPoints);

        return new PartySnapshot
        {
            PartyId = party.StringId,
            Party = party,
            MemberRoster = memberRoster,
            Fingerprint = Fingerprint(party.MemberRoster),
            LeaderHero = leaderHero,
            HeroHitPoints = heroHitPoints,
        };
    }

    private static PartySnapshot Capture(
        MobileParty party,
        PartyBehaviorUpdateData behavior)
    {
        PartySnapshot snapshot = Capture(party);
        snapshot.HasBehavior = true;
        snapshot.Behavior = behavior;
        return snapshot;
    }

    private static bool CanStageExactBattleParty(MobileParty party) =>
        party != null &&
        party.IsActive &&
        party.MapEvent == null &&
        party.CurrentSettlement == null;

    private static bool TryGetFixtureTroopCount(
        PartySnapshot snapshot,
        int healthyTarget,
        out int fixtureTroops,
        out string error)
    {
        int healthyHeroes = 0;
        bool hasHealthyLeader = false;
        foreach (TroopRosterElement element in snapshot.MemberRoster)
        {
            if (!element.Character.IsHero) continue;

            int healthy = Math.Max(0, element.Number - element.WoundedNumber);
            healthyHeroes += healthy;
            if (element.Character.HeroObject == snapshot.Party.LeaderHero && healthy > 0)
                hasHealthyLeader = true;
        }

        if (!hasHealthyLeader)
        {
            fixtureTroops = 0;
            error = $"Party {snapshot.PartyId} must have a healthy leader before staging the battle.";
            return false;
        }
        if (healthyHeroes > healthyTarget)
        {
            fixtureTroops = 0;
            error = $"Party {snapshot.PartyId} has {healthyHeroes} healthy heroes, more than target {healthyTarget}.";
            return false;
        }

        fixtureTroops = healthyTarget - healthyHeroes;
        error = null;
        return true;
    }

    private static void SetExactRoster(
        PartySnapshot snapshot,
        CharacterObject fixtureTroop,
        int fixtureTroops)
    {
        TroopRoster roster = snapshot.Party.MemberRoster;
        ClearRoster(roster);
        foreach (TroopRosterElement element in snapshot.MemberRoster)
        {
            if (!element.Character.IsHero) continue;

            roster.AddToCounts(
                element.Character,
                element.Number,
                false,
                element.WoundedNumber,
                element.Xp,
                true);
        }
        if (fixtureTroops > 0)
            roster.AddToCounts(fixtureTroop, fixtureTroops);
    }

    private static TroopRosterElement[] CopyRoster(TroopRoster roster)
    {
        var copy = new TroopRosterElement[roster.Count];
        for (int index = 0; index < roster.Count; index++)
            copy[index] = roster.GetElementCopyAtIndex(index);

        return copy;
    }

    private static bool Restore(PartySnapshot snapshot)
    {
        bool restoredDeadHero = false;
        TroopRoster roster = snapshot.Party.MemberRoster;
        ClearRoster(roster);

        foreach (TroopRosterElement element in snapshot.MemberRoster)
        {
            if (element.Character.IsHero
                && element.Character.HeroObject?.IsDead == true)
            {
                restoredDeadHero = true;
            }
            roster.AddToCounts(
                element.Character,
                element.Number,
                false,
                element.WoundedNumber,
                element.Xp,
                true);
        }

        foreach (KeyValuePair<Hero, int> hero in snapshot.HeroHitPoints)
        {
            if (!hero.Key.IsDead)
                hero.Key.HitPoints = hero.Value;
        }
        if (snapshot.Party.LeaderHero != snapshot.LeaderHero)
            snapshot.Party.ChangePartyLeader(snapshot.LeaderHero);

        return restoredDeadHero;
    }

    private static bool IsPartyStateRestored(PartySnapshot snapshot) =>
        Fingerprint(snapshot.Party.MemberRoster) == snapshot.Fingerprint &&
        snapshot.Party.LeaderHero == snapshot.LeaderHero &&
        snapshot.HeroHitPoints.All(hero => hero.Key.HitPoints == hero.Value);

    private static void ClearRoster(TroopRoster roster)
    {
        for (int index = roster.Count - 1; index >= 0; index--)
        {
            TroopRosterElement element = roster.GetElementCopyAtIndex(index);
            roster.AddToCountsAtIndex(
                index,
                -element.Number,
                -element.WoundedNumber,
                0,
                false);
        }
        roster.RemoveZeroCounts();
    }

    private static bool RestoreBehavior(PartySnapshot snapshot)
    {
        if (!snapshot.HasBehavior) return true;
        if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
            return false;

        snapshot.Party.Position = snapshot.Behavior.PartyPosition;
        if (!behaviorSnapshot.TryApply(snapshot.Party, snapshot.Behavior, out _))
            return false;

        MessageBroker.Instance.Publish(
            typeof(LargeBattleRosterFixtureCommands),
            new PartyBehaviorChangeAttempted(
                snapshot.Party,
                forcePosition: true,
                isCurrentlyAtSea: snapshot.Behavior.IsCurrentlyAtSea));
        return true;
    }

    private static string FormatState(
        string state,
        MobileParty firstParty,
        MobileParty secondParty,
        int? exactHealthyCount)
    {
        var output = new StringBuilder();
        output.AppendLine(
            $"LARGE_BATTLE_ROSTER_FIXTURE state={state}|" +
            $"exactHealthy={exactHealthyCount?.ToString() ?? "none"}");
        AppendPartyState(output, firstParty);
        AppendPartyState(output, secondParty);
        return output.ToString().TrimEnd();
    }

    private static void AppendPartyState(
        StringBuilder output,
        MobileParty party)
    {
        TroopRoster roster = party.MemberRoster;
        int total = 0;
        int wounded = 0;
        for (int index = 0; index < roster.Count; index++)
        {
            TroopRosterElement element =
                roster.GetElementCopyAtIndex(index);
            total += element.Number;
            wounded += element.WoundedNumber;
        }

        Hero leader = party.LeaderHero;

        output.AppendLine(
            $"party={party.StringId}|total={total}|wounded={wounded}|healthy={total - wounded}|" +
            $"leader={leader?.StringId ?? "none"}|leaderHitPoints={leader?.HitPoints.ToString() ?? "none"}|" +
            $"position={party.Position.X:R},{party.Position.Y:R},{party.Position.IsOnLand}|" +
            $"moveMode={party.PartyMoveMode}|" +
            $"fingerprint={Fingerprint(roster)}");
    }

    private static string Fingerprint(TroopRoster roster)
    {
        var content = new StringBuilder();
        var elements = new List<TroopRosterElement>();
        for (int index = 0; index < roster.Count; index++)
            elements.Add(roster.GetElementCopyAtIndex(index));

        foreach (TroopRosterElement element in elements
                     .OrderBy(
                         value => value.Character.StringId,
                         StringComparer.Ordinal))
        {
            content.Append(element.Character.StringId);
            content.Append('|');
            content.Append(element.Number);
            content.Append('|');
            content.Append(element.WoundedNumber);
            content.Append('|');
            content.Append(element.Xp);
            content.AppendLine();
        }

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(
                Encoding.UTF8.GetBytes(content.ToString()));
            var result = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
                result.Append(value.ToString("x2"));
            return result.ToString();
        }
    }
}
#endif
