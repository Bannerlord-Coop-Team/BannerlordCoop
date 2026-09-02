using Common.Commands;
using Autofac;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
namespace GameInterface.Services.Villages.Commands;

public class BesiegerCampDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    /// <param name="objectManager">Resolved ObjectManager, will be null if unable to resolve</param>
    /// <returns>True if ObjectManager was resolved, otherwise False</returns>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    // coop.debug.besiegercamp.set_number_of_troops_killed_on_side
    /// <summary>
    /// Set the number of tropps killed
    /// </summary>
    /// <param name="args">first arg : besiegerCampId ; second arg : value</param>
    /// <returns>Result of the operation as a string</returns>
    public sealed class SetBesiegerCampNumberOfTroopsKilledOnSideCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.besiegercamp";

        public string Name => "set_number_of_troops_killed_on_side";

        public string Description => "Sets number of troops killed on side for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("besiegerCampId", "The besieger camp id."),
            new ExpectedArgs("value", "The value."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string besiegerCampId = args[0];
            string troopsValueString = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(besiegerCampId, out BesiegerCamp besiegerCamp) == false)
            {
                return Failed($"BesiegerCamp with ID: '{besiegerCampId}' not found");
            }

            if (int.TryParse(troopsValueString, out int troopsValue) == false)
            {
                return Failed($"Argument2: {troopsValueString} is not a int.");
            }

            besiegerCamp.NumberOfTroopsKilledOnSide = troopsValue;

            return Succeeded($"BesiegerCamp NumberOfTroopsKilledOnSide has changed to: {besiegerCamp.NumberOfTroopsKilledOnSide}");

        }
    }

    // coop.debug.besiegercamp.set_progress
    /// <summary>
    /// Set siege preparations progress
    /// </summary>
    /// <param name="args">first arg : besiegerCampId ; second arg : value</param>
    /// <returns>Result of the operation as a string</returns>
    public sealed class SetBesiegerCampPreparationsProgressCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.besiegercamp";

        public string Name => "set_progress";

        public string Description => "Sets progress for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("besiegerCampId", "The besieger camp id."),
            new ExpectedArgs("progress", "The progress."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string besiegerCampId = args[0];
            string percentageValueString = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(besiegerCampId, out BesiegerCamp besiegerCamp) == false)
            {
                return Failed($"BesiegerCamp with ID: '{besiegerCampId}' not found");
            }

            if (float.TryParse(percentageValueString, out float progressPercentage) == false)
            {
                return Failed($"Argument2: {percentageValueString} is not a int.");
            }

            besiegerCamp.SiegeEngines.SiegePreparations.SetProgress(progressPercentage);

            return Succeeded($"BesiegerCamp preparations progress has changed to: {besiegerCamp.SiegeEngines.SiegePreparations.Progress}");

        }
    }

    // coop.debug.besiegercamp.set_siege_strategy
    /// <summary>
    /// Set the siege strategy for a besieger camp
    /// </summary>
    /// <param name="args">first arg: besiegerCampId; second arg: strategyId</param>
    /// <returns>Result of the operation as a string</returns>
    public sealed class SetBesiegerCampSiegeStrategyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.besiegercamp";

        public string Name => "set_siege_strategy";

        public string Description => "Sets siege strategy for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("besiegerCampId", "The besieger camp id."),
            new ExpectedArgs("strategyId", "The strategy id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            string getPossibleStragegyIds() => string.Join(Environment.NewLine, SiegeStrategy.All.Select(x => x.StringId));
            string idTipMsg = $"{Environment.NewLine}SiegeStrategy Id must be one of the following:{getPossibleStragegyIds()}";


            string besiegerCampId = args[0];
            string strategyId = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var besiegerCamp) == false)
            {
                return Failed($"BesiegerCamp with ID: '{besiegerCampId}' not found");
            }

            // Attempt to create or retrieve the SiegeStrategy based on the strategyId
            SiegeStrategy siegeStrategy = SiegeStrategy.All.FirstOrDefault(x => string.Equals(x.StringId, strategyId, StringComparison.OrdinalIgnoreCase));
            if (siegeStrategy == null)
            {
                return Failed($"Invalid SiegeStrategy ID :'{strategyId}'{idTipMsg}");
            }

            // Assign the strategy to the besieger camp
            besiegerCamp.SiegeStrategy = siegeStrategy;

            return Succeeded($"SiegeStrategy for BesiegerCamp {besiegerCampId} has been set to: {siegeStrategy.StringId}");

        }
    }

    // coop.debug.besiegerCamp.set_leader_party
    /// <summary>
    /// Sets the leader party field of a specific besieger camp.
    /// </summary>
    /// <param name="args">besiegerCampId and the partyId to set</param>
    /// <returns>Result of the operation as a string</returns>
    public sealed class SetBesiegerCampLeaderPartyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.besiegercamp";

        public string Name => "set_leader_party";

        public string Description => "Sets leader party for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("besiegerCampId", "The besieger camp id."),
            new ExpectedArgs("partyId", "The party id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string besiegerCampId = args[0];
            string partyId = args[1];

            if (TryGetObjectManager(out var objectManager) == false)
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (objectManager.TryGetObject(besiegerCampId, out BesiegerCamp besiegerCamp) == false)
            {
                return Failed($"{nameof(BesiegerCamp)} with ID: '{besiegerCampId}' not found");
            }

            if (objectManager.TryGetObject(partyId, out MobileParty party) == false)
            {
                return Failed($"{nameof(MobileParty)} with ID: '{partyId}' not found");
            }

            besiegerCamp._leaderParty = party;

            return Succeeded($"{nameof(BesiegerCamp._leaderParty)} has changed to: {besiegerCamp._leaderParty.Name} party with ID: {besiegerCamp._leaderParty.StringId}");

        }
    }

    // coop.debug.besiegercamp.add_party
    /// <summary>
    /// Adds a party as a besieger party to a besieger camp.
    /// </summary>
    /// <param name="args">besiegerCampId and the partyId to add</param>
    /// <returns>Result of the operation as a string</returns>
    public sealed class AddPartyToBesiegerCampCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.besiegercamp";

        public string Name => "add_besieger_party";

        public string Description => "Adds besieger party for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("besiegerCampId", "The besieger camp id."),
            new ExpectedArgs("partyId", "The party id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string besiegerCampId = args[0];
            string partyId = args[1];

            if (!TryGetObjectManager(out var objectManager))
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (!objectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var besiegerCamp))
            {
                return Failed($"BesiegerCamp with ID: '{besiegerCampId}' not found");
            }

            if (!objectManager.TryGetObject<MobileParty>(partyId, out var mobileParty))
            {
                return Failed($"MobileParty with ID: '{partyId}' not found");
            }

            besiegerCamp._besiegerParties.Add(mobileParty);

            return Succeeded($"MobileParty {partyId} added to BesiegerCamp {besiegerCampId}");

        }
    }

    // coop.debug.besiegercamp.remove_party
    /// <summary>
    /// Removes a besieger party from a besieger camp.
    /// </summary>
    /// <param name="args">besiegerCampId and the partyId to remove</param>
    /// <returns>Result of the operation as a string</returns>
    public sealed class RemovePartyFromBesiegerCampCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.besiegercamp";

        public string Name => "remove_party";

        public string Description => "Removes party for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("besiegerCampId", "The besieger camp id."),
            new ExpectedArgs("partyId", "The party id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            string besiegerCampId = args[0];
            string partyId = args[1];

            if (!TryGetObjectManager(out var objectManager))
            {
                return Failed("Unable to resolve ObjectManager");
            }

            if (!objectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var besiegerCamp))
            {
                return Failed($"BesiegerCamp with ID: '{besiegerCampId}' not found");
            }

            if (!objectManager.TryGetObject<MobileParty>(partyId, out var mobileParty))
            {
                return Failed($"MobileParty with ID: '{partyId}' not found");
            }

            if (!besiegerCamp._besiegerParties.Remove(mobileParty))
            {
                return Failed($"MobileParty {partyId} not found in BesiegerCamp {besiegerCampId}");
            }

            return Succeeded($"MobileParty {partyId} removed from BesiegerCamp {besiegerCampId}");

        }
    }
}
