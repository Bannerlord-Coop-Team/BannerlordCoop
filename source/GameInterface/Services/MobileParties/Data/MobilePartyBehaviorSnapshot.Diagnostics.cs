using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Data;

public sealed partial class MobilePartyBehaviorSnapshot
{
    private const int MembershipReportLimit = 8;
    private const int MembershipDetailLimit = 16;
    private readonly HashSet<string> loggedJoinBaselineMembership = new HashSet<string>();

    internal string LastJoinBaselineMembership { get; private set; }
    internal int LoggedJoinBaselineMembershipCount => loggedJoinBaselineMembership.Count;

    // The receive handler already runs on the game thread, behind replayed registrations.
    private void LogJoinBaselineMembership(MobilePartyJoinState[] states, IEnumerable<MobileParty> parties)
    {
        if (loggedJoinBaselineMembership.Count >= MembershipReportLimit) return;

        try
        {
            string report = DescribeJoinBaselineMembership(states, parties);
            LastJoinBaselineMembership = report;
            if (!loggedJoinBaselineMembership.Add(report)) return;

            Logger.Warning(
                "Mobile-party join baseline membership: {Membership}. Identical membership retries are suppressed; report {ReportNumber}/{ReportLimit} until a successful baseline",
                report, loggedJoinBaselineMembership.Count, MembershipReportLimit);
        }
        catch (Exception ex)
        {
            // Diagnostics must not replace the original rejection or prevent the next retry.
            string failure = "membership diagnostics failed: " + ex.GetType().Name;
            if (loggedJoinBaselineMembership.Add(failure))
                Logger.Warning(ex, "Could not inspect rejected mobile-party join baseline membership");
        }
    }

    private string DescribeJoinBaselineMembership(MobilePartyJoinState[] states, IEnumerable<MobileParty> parties)
    {
        var details = new SortedSet<string>(StringComparer.Ordinal);
        int detailCount = 0;
        void Detail(string text)
        {
            detailCount++;
            details.Add(text);
            if (details.Count > MembershipDetailLimit) details.Remove(details.Max);
        }

        var localParties = new HashSet<MobileParty>();
        var localIds = new Dictionary<string, MobileParty>(StringComparer.Ordinal);
        int duplicateMembership = 0, duplicateLocalIds = 0, unregisteredActive = 0, active = 0;
        foreach (MobileParty party in parties)
        {
            if (party == null) continue;
            if (!localParties.Add(party))
            {
                duplicateMembership++;
                Detail("duplicate local membership: " + DescribeJoinParty(party));
                continue;
            }
            if (party.IsActive) active++;
            if (!objectManager.TryGetId(party, out string id) || string.IsNullOrEmpty(id))
            {
                if (party.IsActive)
                {
                    unregisteredActive++;
                    Detail("unregistered active local: " + DescribeJoinParty(party));
                }
            }
            else if (localIds.ContainsKey(id))
            {
                duplicateLocalIds++;
                Detail("duplicate local registry id: " + DescribeJoinParty(party));
            }
            else localIds.Add(id, party);
        }

        var baselineIds = new HashSet<string>(StringComparer.Ordinal);
        var baselineParties = new HashSet<MobileParty>();
        int missing = 0, inactive = 0, absent = 0, duplicateIds = 0, aliases = 0, emptyIds = 0, wrongType = 0;
        foreach (MobilePartyJoinState state in states)
        {
            string id = state.Behavior.MobilePartyId;
            if (string.IsNullOrEmpty(id))
            {
                emptyIds++;
                continue;
            }
            if (!baselineIds.Add(id))
            {
                duplicateIds++;
                Detail("duplicate baseline id: " + DiagnosticValue(id));
                continue;
            }
            // Resolve as object to avoid the registry's per-lookup error on a wrong-type entry.
            if (!objectManager.TryGetObject<object>($"MobileParty_{id}", out var registered) &&
                !objectManager.TryGetObject<object>(id, out registered))
            {
                missing++;
                Detail("missing baseline id: " + DiagnosticValue(id));
                continue;
            }
            if (registered is not MobileParty party)
            {
                wrongType++;
                Detail("wrong-type baseline id: " + DiagnosticValue(id) + " type=" + DiagnosticValue(registered?.GetType().Name));
                continue;
            }
            if (!baselineParties.Add(party))
            {
                aliases++;
                Detail("baseline ids resolve to same party: " + DiagnosticValue(id) + " " + DescribeJoinParty(party));
            }
            if (!party.IsActive)
            {
                inactive++;
                Detail("inactive baseline id: " + DiagnosticValue(id) + " " + DescribeJoinParty(party));
            }
            if (!localParties.Contains(party))
            {
                absent++;
                Detail("baseline object outside local collection: " + DiagnosticValue(id) + " " + DescribeJoinParty(party));
            }
        }

        int extra = 0;
        foreach (MobileParty party in localParties)
        {
            if (!party.IsActive || baselineParties.Contains(party)) continue;
            extra++;
            Detail("active local absent from baseline: " + DescribeJoinParty(party));
        }

        return $"baselineEntries={states.Length}, localActiveUnique={active}, missing={missing}, inactive={inactive}, outsideCollection={absent}, extraActive={extra}, " +
            $"emptyBaselineIds={emptyIds}, wrongType={wrongType}, duplicateBaselineIds={duplicateIds}, aliasedBaselineIds={aliases}, duplicateLocalMembership={duplicateMembership}, duplicateLocalIds={duplicateLocalIds}, unregisteredActive={unregisteredActive}; " +
            $"mainParty=[{DescribeJoinParty(Campaign.Current?.MainParty)}]; detailsShown={details.Count}/{detailCount} (limit={MembershipDetailLimit}): " +
            string.Join("; ", details);
    }

    private string DescribeJoinParty(MobileParty party)
    {
        if (party == null) return "none";
        objectManager.TryGetId(party, out string id);
        var leader = party.LeaderHero;
        return $"id={DiagnosticValue(id)}, stringId={DiagnosticValue(party.StringId)}, active={party.IsActive}, " +
            $"main={ReferenceEquals(party, Campaign.Current?.MainParty)}, leader={DiagnosticValue(leader?.StringId)}, captive={leader?.IsPrisoner}, settlement={DiagnosticValue(party.CurrentSettlement?.StringId)}";
    }

    private static string DiagnosticValue(string value)
    {
        if (value == null) return "none";
        // Bound identifiers too, so even a malformed baseline cannot produce an oversized log.
        if (value.Length > 80) value = value.Substring(0, 80) + "...";
        return value.Replace('\r', ' ').Replace('\n', ' ');
    }
}
