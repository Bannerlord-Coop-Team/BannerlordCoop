#if DEBUG
using Autofac;
using Common;
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
        public PartySnapshot FirstParty;
        public PartySnapshot SecondParty;
    }

    private sealed class PartySnapshot
    {
        public MobileParty Party;
        public TroopRosterElement[] MemberRoster;
        public string Fingerprint;
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
            FormatState("active", firstParty, secondParty);
    }

    [CommandLineArgumentFunction("large_battle_roster_status", "coop.debug.mobileparty")]
    public static string Status(List<string> args)
    {
        if (args.Count != 2)
        {
            return "Usage: coop.debug.mobileparty.large_battle_roster_status " +
                   "<firstPartyId> <secondPartyId>";
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
            secondParty);
    }

    [CommandLineArgumentFunction("large_battle_roster_restore", "coop.debug.mobileparty")]
    public static string Restore(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mobileparty.large_battle_roster_restore";
        if (fixture == null)
            return "No large-battle roster fixture is pending restoration.";

        LargeBattleRosterFixture activeFixture = fixture;
        Restore(activeFixture.FirstParty);
        Restore(activeFixture.SecondParty);

        string firstFingerprint = Fingerprint(
            activeFixture.FirstParty.Party.MemberRoster);
        string secondFingerprint = Fingerprint(
            activeFixture.SecondParty.Party.MemberRoster);
        if (firstFingerprint != activeFixture.FirstParty.Fingerprint
            || secondFingerprint != activeFixture.SecondParty.Fingerprint)
        {
            return "Large-battle roster restoration did not reproduce the original fingerprints.\n" +
                   FormatState(
                       "restore-failed",
                       activeFixture.FirstParty.Party,
                       activeFixture.SecondParty.Party);
        }

        fixture = null;
        return
            "LARGE_BATTLE_ROSTER_FIXTURE_RESTORED\n" +
            FormatState(
                "none",
                activeFixture.FirstParty.Party,
                activeFixture.SecondParty.Party);
    }

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

    private static PartySnapshot Capture(MobileParty party)
    {
        return new PartySnapshot
        {
            Party = party,
            MemberRoster = CopyRoster(party.MemberRoster),
            Fingerprint = Fingerprint(party.MemberRoster),
        };
    }

    private static TroopRosterElement[] CopyRoster(TroopRoster roster)
    {
        var copy = new TroopRosterElement[roster.Count];
        for (int index = 0; index < roster.Count; index++)
            copy[index] = roster.GetElementCopyAtIndex(index);

        return copy;
    }

    private static void Restore(PartySnapshot snapshot)
    {
        TroopRoster roster = snapshot.Party.MemberRoster;
        for (int index = roster.Count - 1; index >= 0; index--)
        {
            TroopRosterElement element =
                roster.GetElementCopyAtIndex(index);
            roster.AddToCountsAtIndex(
                index,
                -element.Number,
                -element.WoundedNumber,
                0,
                false);
        }
        roster.RemoveZeroCounts();

        foreach (TroopRosterElement element in snapshot.MemberRoster)
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

    private static string FormatState(
        string state,
        MobileParty firstParty,
        MobileParty secondParty)
    {
        var output = new StringBuilder();
        output.AppendLine(
            $"LARGE_BATTLE_ROSTER_FIXTURE state={state}");
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

        output.AppendLine(
            $"party={party.StringId}|total={total}|wounded={wounded}|healthy={total - wounded}|" +
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
