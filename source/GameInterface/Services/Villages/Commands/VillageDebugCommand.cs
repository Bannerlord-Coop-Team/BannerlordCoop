using Common.Commands;
using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
namespace GameInterface.Services.Villages.Commands;

internal class VillageDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    /// <summary>
    /// Finds a specific village in game.
    /// </summary>
    /// <param name="villageId">string id of the village to search</param>
    /// <returns>Village or null.</returns>
    private static Village findVillage(string villageId)
    {
        List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements
            .Where(settlement => settlement.IsVillage).ToList();
        Village village = settlements.Find(e => e.Village.StringId == villageId)?.Village;
        return village;
    }

    // coop.debug.village.list
    /// <summary>
    /// Lists all the villages
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the villages</returns>
    public sealed class ListVillagesCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.village";

        public string Name => "list";

        public string Description => "Lists the relevant state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            StringBuilder stringBuilder = new StringBuilder();

            List<Settlement> settlements = Campaign.Current.CampaignObjectManager.Settlements
                .Where(settlement => settlement.IsVillage).ToList();

            settlements.ForEach((settlement) =>
            {
                Village v = settlement.Village;
                stringBuilder.Append(string.Format("ID: '{0}'\nName: '{1}'\n", v.StringId, v.Name));
            });

            return Succeeded(stringBuilder.ToString());

        }
    }

    /// coop.debug.village.info castle_village_comp_K7_2
    /// coop.debug.village.info village_ES1_3
    /// <summary>
    /// Gets information on a specific village
    /// </summary>
    /// <param name="args">vilage ID to lookup</param>
    /// <returns>Information regarding the village.</returns>
    public sealed class InfoCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.village";

        public string Name => "info";

        public string Description => "Shows the relevant state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("villageId", "The village id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            Village village = findVillage(args[0]);

            if (village == null)
            {
                return Failed(string.Format("ID: '{0}' not found", args[0]));
            }


            StringBuilder sb = new();

            sb.AppendFormat("ID: '{0}'\n", args[0]);
            sb.AppendFormat("Name: '{0}'\n", village.Name);
            sb.AppendFormat("Owner: '{0}'\n", village.Owner.Name);
            sb.AppendFormat("State: '{0}'\n", village.VillageState.ToString());
            sb.AppendFormat("Hearth: '{0}'\n", village.Hearth);
            sb.AppendFormat("TradeTaxAccumulated: '{0}'\n", village.TradeTaxAccumulated);
            sb.AppendFormat("LastDemandStatisifiedTime: '{0}'\n", village.LastDemandSatisfiedTime);

            return Succeeded(sb.ToString());

        }
    }

    // coop.debug.village.set_state castle_village_comp_K7_2 BeingRaided
    /// <summary>
    /// Sets the VillageState of a specific Village.
    /// </summary>
    /// <param name="args">villageID and the state to set</param>
    /// <returns>information if it changed</returns>
    public sealed class SetVillageStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.village";

        public string Name => "set_state";

        public string Description => "Sets state for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("villageId", "The village id."),
            new ExpectedArgs("state", "The state."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("This command can only be used by the server for debugging purposes.");


            Village village = findVillage(args[0]);

            if (village == null)
            {
                return Failed(string.Format("ID: '{0}' not found", args[0]));
            }


            if(!Enum.TryParse(args[1], out Village.VillageStates villageState))
            {
                return Failed(string.Format("InvalidVillageState: '{0}' not found", args[0]));
            }
            village.VillageState = villageState;

            return Succeeded(string.Format("VillageState has changed to: {0}", villageState));

        }
    }


    // coop.debug.village.set_hearth castle_village_comp_K7_2 2.0
    /// <summary>
    /// sets the hearth float value for a village.
    /// </summary>
    /// <param name="args">the village and hearth value float</param>
    /// <returns>string output if success</returns>
    public sealed class SetVillageHearthCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.village";

        public string Name => "set_hearth";

        public string Description => "Sets hearth for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("villageId", "The village id."),
            new ExpectedArgs("hearth", "The hearth."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("This command can only be used by the server for debugging purposes.");


            Village village = findVillage(args[0]);

            if (village == null)
            {
                return Failed(string.Format("ID: '{0}' not found", args[0]));
            }

            float hearth = 0.0f;
            try
            {
                hearth = float.Parse(args[1]);
            }catch(Exception)
            {
                return Failed(string.Format("Failed to parse the value: {0}", hearth));
            }

            village.Hearth = hearth;

            return Succeeded(string.Format("Hearth has changed to to: {0}", hearth));

        }
    }

    // coop.debug.village.set_trade_tax_acc castle_village_comp_K7_2 500
    /// <summary>
    /// sets the tradetaxaccumulated value for a village.
    /// </summary>
    /// <param name="args">the village and tradetaxaccumulated value float</param>
    /// <returns>string output if success</returns>
    public sealed class SetTradeTaxAccumulatedCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.village";

        public string Name => "set_trade_tax_acc";

        public string Description => "Sets trade tax acc for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("villageId", "The village id."),
            new ExpectedArgs("tradeTax", "The trade tax."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("This command can only be used by the server for debugging purposes.");


            Village village = findVillage(args[0]);

            if (village == null)
            {
                return Failed(string.Format("ID: '{0}' not found", args[0]));
            }

            int tradeTaxAccumulated = 0;
            try
            {
                tradeTaxAccumulated = int.Parse(args[1]);
            }
            catch (Exception)
            {
                return Failed(string.Format("Failed to parse the value: {0}", tradeTaxAccumulated));
            }

            village.TradeTaxAccumulated = tradeTaxAccumulated;

            return Succeeded(string.Format("Hearth has changed to to: {0}", tradeTaxAccumulated));

        }
    }


    // coop.debug.village.set_demand_time castle_village_comp_K7_2 2.0
    /// <summary>
    /// sets the LastDemandTimeSatisified
    /// </summary>
    /// <param name="args">the village and village last demand time value</param>
    /// <returns>string output if success</returns>
    public sealed class SetLastDemandTimeSatisifiedCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.village";

        public string Name => "set_demand_time";

        public string Description => "Sets demand time for co-op debugging.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("villageId", "The village id."),
            new ExpectedArgs("demandTime", "The demand time."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient)
                return Failed("This command can only be used by the server for debugging purposes.");


            Village village = findVillage(args[0]);

            if (village == null)
            {
                return Failed(string.Format("ID: '{0}' not found", args[0]));
            }

            float lastDemandTime = 0.0f;
            try
            {
                lastDemandTime = float.Parse(args[1]);
            }
            catch (Exception)
            {
                return Failed(string.Format("Failed to parse the value: {0}", lastDemandTime));
            }

            village.LastDemandSatisfiedTime = lastDemandTime;

            return Succeeded(string.Format("Hearth has changed to to: {0}", lastDemandTime));

        }
    }
}
