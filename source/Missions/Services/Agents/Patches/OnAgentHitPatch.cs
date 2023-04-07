using HarmonyLib;
using Missions.Services.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Patches
{
    [HarmonyPatch(typeof(Mission), "OnAgentHit")]
    internal static class DamangePatch
    {
        private static bool Prefix(ref Agent affectorAgent)
        {
            // Only allow damage from controlled agents
            if (!NetworkAgentRegistry.Instance.IsControlled(affectorAgent)) return false;

            return true;
        }
    }
}
