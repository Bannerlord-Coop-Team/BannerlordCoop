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
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Party.Commands;

internal static class LargeBattleRosterFixtureCommands
{
    internal const int MaximumTargetTroopsPerParty = 2000;
    private const string FixtureTroopId = "imperial_recruit";
    private static LargeBattleRosterFixture fixture;

    private sealed class LargeBattleRosterFixture
    {
        public Campaign Campaign;
        public PartySnapshot FirstParty;
        public PartySnapshot SecondParty;
    }

    private sealed class PartySnapshot
    {
        public string PartyId;
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
            || !int.TryParse(args[2], out int targetTroopsPerParty)
            || targetTroopsPerParty < 1
            || targetTroopsPerParty > MaximumTargetTroopsPerParty)
        {
            return "Usage: coop.debug.mobileparty.large_battle_roster_begin " +
                   $"<firstPartyId> <secondPartyId> <targetTroopsPerParty:1-{MaximumTargetTroopsPerParty}>";
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

        TryBegin(
            firstParty,
            secondParty,
            targetTroopsPerParty,
            out string result);
        return result;
    }

    internal static bool TryBegin(
        MobileParty firstParty,
        MobileParty secondParty,
        int targetTroopsPerParty,
        out string result)
    {
        if (!ModInformation.IsServer)
        {
            result = "Run this command on the server.";
            return false;
        }
        if (targetTroopsPerParty < 1
            || targetTroopsPerParty > MaximumTargetTroopsPerParty)
        {
            result =
                $"Target troops per party must be from 1 to {MaximumTargetTroopsPerParty}.";
            return false;
        }
        if (fixture != null)
        {
            result = "A large-battle roster fixture is already pending restoration.";
            return false;
        }
        if (firstParty == null || secondParty == null)
        {
            result = "The fixture requires two available parties.";
            return false;
        }
        if (firstParty == secondParty)
        {
            result = "The fixture requires two distinct parties.";
            return false;
        }
        int firstTotal = firstParty.MemberRoster.TotalManCount;
        int secondTotal = secondParty.MemberRoster.TotalManCount;
        if (firstTotal > targetTroopsPerParty
            || secondTotal > targetTroopsPerParty)
        {
            result =
                $"Both parties must have at most {targetTroopsPerParty} troops before the fixture starts. " +
                $"Actual totals: {firstParty.StringId}={firstTotal}, {secondParty.StringId}={secondTotal}.";
            return false;
        }
        if (!TryGetObjectManager(out IObjectManager objectManager))
        {
            result = "Unable to resolve ObjectManager.";
            return false;
        }
        if (!objectManager.TryGetObject(
                FixtureTroopId,
                out CharacterObject fixtureTroop))
        {
            result = $"Unable to resolve fixture troop {FixtureTroopId}.";
            return false;
        }

        var activeFixture = new LargeBattleRosterFixture
        {
            Campaign = Campaign.Current,
            FirstParty = Capture(firstParty),
            SecondParty = Capture(secondParty),
        };
        fixture = activeFixture;

        try
        {
            int firstAdded = targetTroopsPerParty - firstTotal;
            int secondAdded = targetTroopsPerParty - secondTotal;
            if (firstAdded > 0)
                firstParty.MemberRoster.AddToCounts(fixtureTroop, firstAdded);
            if (secondAdded > 0)
                secondParty.MemberRoster.AddToCounts(fixtureTroop, secondAdded);
        }
        catch (Exception exception)
        {
            try
            {
                Restore(activeFixture.FirstParty);
                Restore(activeFixture.SecondParty);
                fixture = null;
                result = $"Large-battle roster fixture setup failed and was restored: {exception.Message}";
            }
            catch (Exception restoreException)
            {
                result =
                    $"Large-battle roster fixture setup failed: {exception.Message}. " +
                    $"Automatic restoration also failed: {restoreException.Message}. Run the abort command.";
            }
            return false;
        }

        result =
            $"LARGE_BATTLE_ROSTER_FIXTURE_STARTED troop={FixtureTroopId} " +
            $"targetPerParty={targetTroopsPerParty}\n" +
            FormatState("active", firstParty, secondParty);
        return true;
    }

    [CommandLineArgumentFunction("large_battle_roster_status", "coop.debug.mobileparty")]
    public static string Status(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
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
        return RestoreActiveFixture();
    }

    [CommandLineArgumentFunction("large_battle_roster_abort", "coop.debug.mobileparty")]
    public static string Abort(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mobileparty.large_battle_roster_abort";
        return AbortActiveFixture();
    }

    internal static string AbortActiveFixture()
    {
        if (fixture == null)
            return "No large-battle roster fixture is pending restoration.";

        MobileParty firstParty = fixture.FirstParty.Party;
        MobileParty secondParty = fixture.SecondParty.Party;
        MapEvent mapEvent = firstParty?.MapEvent;
        bool finalizedMapEvent = mapEvent != null
            && mapEvent == secondParty?.MapEvent
            && !mapEvent.IsFinalized;
        string restoration;
        try
        {
            if (finalizedMapEvent)
                mapEvent.FinalizeEvent();
        }
        finally
        {
            restoration = RestoreActiveFixture();
        }

        return $"LARGE_BATTLE_ROSTER_FIXTURE_ABORTED finalizedMapEvent={finalizedMapEvent}\n" +
               restoration;
    }

    internal static string RestoreActiveFixture()
    {
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
            return firstError;
        }
        if (!TryResolveSnapshotParty(
                objectManager,
                activeFixture.SecondParty,
                out string secondError))
        {
            return secondError;
        }

        bool firstRestoredDeadHero =
            Restore(activeFixture.FirstParty);
        bool secondRestoredDeadHero =
            Restore(activeFixture.SecondParty);
        bool restoredDeadHero =
            firstRestoredDeadHero || secondRestoredDeadHero;
        string warning = restoredDeadHero
            ? "WARNING: the restored roster contains a dead hero and may be invalid.\n"
            : string.Empty;

        string firstFingerprint = Fingerprint(
            activeFixture.FirstParty.Party.MemberRoster);
        string secondFingerprint = Fingerprint(
            activeFixture.SecondParty.Party.MemberRoster);
        if (firstFingerprint != activeFixture.FirstParty.Fingerprint
            || secondFingerprint != activeFixture.SecondParty.Fingerprint)
        {
            return "Large-battle roster restoration did not reproduce the original fingerprints.\n" +
                   warning +
                   FormatState(
                       "restore-failed",
                       activeFixture.FirstParty.Party,
                       activeFixture.SecondParty.Party);
        }

        fixture = null;
        return
            "LARGE_BATTLE_ROSTER_FIXTURE_RESTORED\n" +
            warning +
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

    private static bool TryResolveSnapshotParty(
        IObjectManager objectManager,
        PartySnapshot snapshot,
        out string error)
    {
        if (objectManager.TryGetObject(
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
        return new PartySnapshot
        {
            PartyId = party.StringId,
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

    private static bool Restore(PartySnapshot snapshot)
    {
        bool restoredDeadHero = false;
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

        return restoredDeadHero;
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
