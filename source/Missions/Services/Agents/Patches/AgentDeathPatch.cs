using HarmonyLib;
using SandBox.Conversation.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.MountAndBlade.Agent;

namespace Missions.Services.Agents.Patches
{
    [HarmonyPatch(typeof(Agent), "Die")]
    public class AgentDeathPatch
    {
        public static void Postfix(Blow b, KillInfo overrideKillInfo, ref Agent __instance)
        {
            if (__instance.IsMainAgent)
            {
                foreach (MissionBehavior missionBehavior in Mission.Current.MissionBehaviors)
                {
                    if (missionBehavior.GetType().Equals(typeof(CoopArenaController)))
                    {
                        //Add delay

                        (missionBehavior as CoopArenaController).RespawnPlayer();
                        return;
                    }
                }
            }
        }
    }
}
