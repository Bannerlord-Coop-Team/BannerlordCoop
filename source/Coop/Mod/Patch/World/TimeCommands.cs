using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Patch.World
{
    class TimeCommands
    {
        private const string sGroupName = "coop";
        private const string sTestGroupName = "test";
        private static Dictionary<string, CampaignTimeControlMode> TimeControlModes = new Dictionary<string, CampaignTimeControlMode>();
        static TimeCommands()
        {
            foreach(var e in Enum.GetValues(typeof(CampaignTimeControlMode)).Cast<CampaignTimeControlMode>())
            {
                TimeControlModes.Add(e.ToString(),e);
            }
        }

        /// <summary>
        /// Sets time speed for to the speed given as argument.
        /// </summary>
        /// <param name="parameters">Expects a speed value to set speed</param>
        /// <returns>Changed value in TimeControlMode.</returns>
        [CommandLineFunctionality.CommandLineArgumentFunction("set_time_speed", sTestGroupName)]
        public static string SetTimeSpeed(List<string> parameters)
        {
            if (parameters.Count != 1 || !int.TryParse(parameters[0], out int timeSpeed) || timeSpeed < 0 || timeSpeed > 2)
            {
                return $"Usage: \"{sTestGroupName}.set_time_speed [time_speed (0-2)].";
            }

            var oldTimeControlMode = Campaign.Current.TimeControlMode;
            Campaign.Current.SetTimeSpeed(timeSpeed);

            return $"Campaign TimeControlMode changed from {oldTimeControlMode} to {Campaign.Current.TimeControlMode}";
        }

        /// <summary>
        /// Gets time control mode for the Campaign.
        /// </summary>
        /// <returns>TimeControlMode value.</returns>
        [CommandLineFunctionality.CommandLineArgumentFunction("get_time_speed", sTestGroupName)]
        public static string GetTimeSpeed(List<string> parameters)
        {
            return $"Campaign TimeControlMode is {Campaign.Current.TimeControlMode}";
        }
    }
}
