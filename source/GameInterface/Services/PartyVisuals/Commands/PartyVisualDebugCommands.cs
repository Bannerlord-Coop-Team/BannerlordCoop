using Common;
using SandBox.View.Map.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.PartyVisuals.Commands;

internal class PartyVisualDebugCommands
{
#if DEBUG
    private const string FixturePartyIdPrefix = "issue2938_visual_fixture_";
    private static readonly List<MobileParty> stagedParties = new();
    private static int fixtureBaselineEligiblePartyCount = -1;
#endif

    [CommandLineArgumentFunction("buffer_state", "coop.debug.partyvisuals")]
    public static string BufferState(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.partyvisuals.buffer_state";

        var manager = MobilePartyVisualManager.Current;
        if (manager == null)
            return "Mobile party visual manager is unavailable.";

        int visualCount = manager._visualsFlattened.Count;
        int bufferCapacity = manager._dirtyPartiesList.Length;
        int dirtyCount = manager._dirtyPartyVisualCount;
        int campaignPartyCount = Campaign.Current?.MobileParties?.Count ?? 0;
        string structuredState = JsonSerializer.Serialize(new
        {
            visualCount,
            bufferCapacity,
            dirtyCount,
            campaignPartyCount,
        });

        return $"visualCount={visualCount} bufferCapacity={bufferCapacity} dirtyCount={dirtyCount} " +
               $"campaignPartyCount={campaignPartyCount}" + Environment.NewLine +
               $"LIVE_TEST_JSON={structuredState}";
    }

#if DEBUG
    [CommandLineArgumentFunction("fixture_state", "coop.debug.partyvisuals")]
    public static string FixtureState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.partyvisuals.fixture_state";

        return GetFixtureState(includeBaseline: true);
    }

    [CommandLineArgumentFunction("stage_over_limit_fixture", "coop.debug.partyvisuals")]
    public static string StageOverLimitFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "stage_over_limit_fixture must be run on the server.";

        if (args.Count != 2 ||
            !int.TryParse(args[0], out int targetEligiblePartyCount) ||
            targetEligiblePartyCount < 2500)
        {
            return "Usage: coop.debug.partyvisuals.stage_over_limit_fixture <targetEligiblePartyCount>=2500+ <settlementId>";
        }

        if (stagedParties.Count != 0 || GetLiveFixturePartyCount() != 0)
            return "The party-visual fixture is already staged.";

        Settlement settlement = Settlement.All.FirstOrDefault(candidate => candidate.StringId == args[1]);
        if (settlement == null)
            return $"Settlement '{args[1]}' was not found.";

        Clan looterClan = Clan.BanditFactions.FirstOrDefault(candidate => candidate.StringId == "looters");
        if (looterClan == null)
            return "The looter clan was not found.";

        fixtureBaselineEligiblePartyCount = GetEligiblePartyCount();
        int partiesToCreate = targetEligiblePartyCount - fixtureBaselineEligiblePartyCount;
        if (partiesToCreate <= 0)
            return GetFixtureState(includeBaseline: true);

        try
        {
            for (int index = 0; index < partiesToCreate; index++)
            {
                MobileParty party = BanditPartyComponent.CreateLooterParty(
                    $"{FixturePartyIdPrefix}{index + 1}",
                    looterClan,
                    settlement,
                    isBossParty: false,
                    pt: null,
                    settlement.GatePosition);
                party.IsVisible = true;
                party.IsInspected = true;
                party.SetMoveModeHold();
                stagedParties.Add(party);
            }
        }
        catch
        {
            RestoreFixtureParties();
            throw;
        }

        return GetFixtureState(includeBaseline: true);
    }

    [CommandLineArgumentFunction("restore_over_limit_fixture", "coop.debug.partyvisuals")]
    public static string RestoreOverLimitFixture(List<string> args)
    {
        if (ModInformation.IsClient)
            return "restore_over_limit_fixture must be run on the server.";

        if (args.Count != 0)
            return "Usage: coop.debug.partyvisuals.restore_over_limit_fixture";

        int removedPartyCount = RestoreFixtureParties();
        string state = GetFixtureState(includeBaseline: false);
        return $"removedPartyCount={removedPartyCount}{Environment.NewLine}{state}";
    }

    private static int RestoreFixtureParties()
    {
        int removedPartyCount = 0;
        List<MobileParty> fixtureParties = MobileParty.All
            .Where(party => party?.StringId?.StartsWith(FixturePartyIdPrefix, StringComparison.Ordinal) == true)
            .ToList();
        for (int index = fixtureParties.Count - 1; index >= 0; index--)
        {
            MobileParty party = fixtureParties[index];
            if (party?.IsActive != true) continue;

            DestroyPartyAction.Apply(null, party);
            removedPartyCount++;
        }

        stagedParties.Clear();
        fixtureBaselineEligiblePartyCount = -1;
        return removedPartyCount;
    }

    private static string GetFixtureState(bool includeBaseline)
    {
        int eligiblePartyCount = GetEligiblePartyCount();
        int liveFixturePartyCount = GetLiveFixturePartyCount();
        string structuredState = JsonSerializer.Serialize(new
        {
            eligiblePartyCount,
            liveFixturePartyCount,
            fixtureBaselineEligiblePartyCount = includeBaseline ? fixtureBaselineEligiblePartyCount : -1,
            campaignPartyCount = Campaign.Current?.MobileParties?.Count ?? 0,
        });

        return $"eligiblePartyCount={eligiblePartyCount} liveFixturePartyCount={liveFixturePartyCount} " +
               $"fixtureBaselineEligiblePartyCount={(includeBaseline ? fixtureBaselineEligiblePartyCount : -1)}" + Environment.NewLine +
               $"LIVE_TEST_JSON={structuredState}";
    }

    private static int GetEligiblePartyCount()
    {
        return MobileParty.All.Count(party =>
            party?.IsActive == true &&
            !party.IsGarrison &&
            !party.IsMilitia);
    }

    private static int GetLiveFixturePartyCount()
    {
        return MobileParty.All.Count(party =>
            party?.IsActive == true &&
            party.StringId?.StartsWith(FixturePartyIdPrefix, StringComparison.Ordinal) == true);
    }
#endif
}
