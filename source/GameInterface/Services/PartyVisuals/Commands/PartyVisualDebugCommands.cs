using Common.Commands;
using Common;
using GameInterface.Services.PartyVisuals.Patches;
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

namespace GameInterface.Services.PartyVisuals.Commands;

internal class PartyVisualDebugCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

#if DEBUG
    private const string FixturePartyIdPrefix = "issue2938_visual_fixture_";
    private static readonly List<MobileParty> stagedParties = new();
    private static int fixtureBaselineEligiblePartyCount = -1;
#endif

    public sealed class BufferStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.party_visuals";

        public string Name => "buffer_state";

        public string Description => "Reports buffer state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");


            var manager = MobilePartyVisualManager.Current;
            if (manager == null)
                return Failed("Mobile party visual manager is unavailable.");

            int visualCount = manager._visualsFlattened.Count;
            int bufferCapacity = manager._dirtyPartiesList.Length;
            int dirtyCount = manager._dirtyPartyVisualCount;
            bool navalManagerActive = NavalMobilePartyVisualManagerPatches.TryGetBufferState(
                out int navalVisualCount,
                out int navalBufferCapacity,
                out int navalDirtyCount);
            int campaignPartyCount = Campaign.Current?.MobileParties?.Count ?? 0;
            string structuredState = JsonSerializer.Serialize(new
            {
                visualCount,
                bufferCapacity,
                dirtyCount,
                navalManagerActive,
                navalVisualCount,
                navalBufferCapacity,
                navalDirtyCount,
                campaignPartyCount,
            });

            return Succeeded($"visualCount={visualCount} bufferCapacity={bufferCapacity} dirtyCount={dirtyCount} " +
                   $"navalManagerActive={navalManagerActive} navalVisualCount={navalVisualCount} " +
                   $"navalBufferCapacity={navalBufferCapacity} navalDirtyCount={navalDirtyCount} " +
                   $"campaignPartyCount={campaignPartyCount}" + Environment.NewLine +
                   $"LIVE_TEST_JSON={structuredState}");
        }
    }

#if DEBUG
    public sealed class FixtureStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.party_visuals";

        public string Name => "fixture_state";

        public string Description => "Reports fixture state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            return Succeeded(GetFixtureState(includeBaseline: true));
        }
    }

    public sealed class StageOverLimitFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.party_visuals";

        public string Name => "stage_over_limit_fixture";

        public string Description => "Runs the stage over limit fixture debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("target_eligible_party_count", "The target eligible party count.", true),
            new ExpectedArgs("settlement_id", "The settlement id.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("stage_over_limit_fixture must be run on the server.");

            if (!int.TryParse(args[0], out int targetEligiblePartyCount) ||
                targetEligiblePartyCount < 2500)
            {
                return Failed("Invalid command argument value.");
            }

            if (stagedParties.Count != 0 || GetLiveFixturePartyCount() != 0)
                return Failed("The party-visual fixture is already staged.");

            Settlement settlement = Settlement.All.FirstOrDefault(candidate => candidate.StringId == args[1]);
            if (settlement == null)
                return Failed($"Settlement '{args[1]}' was not found.");

            Clan looterClan = Clan.BanditFactions.FirstOrDefault(candidate => candidate.StringId == "looters");
            if (looterClan == null)
                return Failed("The looter clan was not found.");

            fixtureBaselineEligiblePartyCount = GetEligiblePartyCount();
            int partiesToCreate = targetEligiblePartyCount - fixtureBaselineEligiblePartyCount;
            if (partiesToCreate <= 0)
                return Succeeded(GetFixtureState(includeBaseline: true));

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
                    stagedParties.Add(party);
                    party.IsVisible = true;
                    party.IsInspected = true;
                    party.SetMoveModeHold();
                }
            }
            catch
            {
                RestoreFixtureParties();
                throw;
            }

            return Succeeded(GetFixtureState(includeBaseline: true));
        }
    }

    public sealed class RestoreOverLimitFixtureCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.party_visuals";

        public string Name => "restore_over_limit_fixture";

        public string Description => "Restores or clears restore over limit fixture.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("restore_over_limit_fixture must be run on the server.");


            int removedPartyCount = RestoreFixtureParties();
            string state = GetFixtureState(includeBaseline: false);
            return Succeeded($"removedPartyCount={removedPartyCount}{Environment.NewLine}{state}");
        }
    }

    private static int RestoreFixtureParties()
    {
        int removedPartyCount = 0;
        List<MobileParty> fixtureParties = GetFixturePartiesForRestore(stagedParties, MobileParty.All);
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

    internal static List<MobileParty> GetFixturePartiesForRestore(
        IEnumerable<MobileParty> retainedParties,
        IEnumerable<MobileParty> campaignParties)
    {
        return retainedParties
            .Concat(campaignParties.Where(party =>
                party?.StringId?.StartsWith(FixturePartyIdPrefix, StringComparison.Ordinal) == true))
            .Distinct()
            .ToList();
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
        return GetLiveFixturePartyCount(stagedParties, MobileParty.All);
    }

    internal static int GetLiveFixturePartyCount(
        IEnumerable<MobileParty> retainedParties,
        IEnumerable<MobileParty> campaignParties)
    {
        return GetFixturePartiesForRestore(retainedParties, campaignParties)
            .Count(party => party?.IsActive == true);
    }
#endif
}
