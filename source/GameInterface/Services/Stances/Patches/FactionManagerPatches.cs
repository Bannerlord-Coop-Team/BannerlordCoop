using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Stances.Patches
{
    [HarmonyPatch]
    internal class FactionManager_AddStance_Patch
    {
        private static bool Prepare()
        {
            return AccessTools.Method(typeof(FactionManager), "AddStance") != null;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(FactionManager), "AddStance");
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            return true;
        }
    }

    [HarmonyPatch]
    internal class FactionManager_RemoveStance_Patch
    {
        private static bool Prepare()
        {
            return AccessTools.Method(typeof(FactionManager), "RemoveStance") != null;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(FactionManager), "RemoveStance");
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch]
    internal class FactionManager_SetStance_Patch
    {
        private static bool Prepare()
        {
            return AccessTools.Method(typeof(FactionManager), "SetStance") != null;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(FactionManager), "SetStance");
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch]
    internal class FactionManager_DeclareAlliance_Patch
    {
        private static bool Prepare()
        {
            return AccessTools.Method(typeof(FactionManager), "DeclareAlliance") != null;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(FactionManager), "DeclareAlliance");
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch]
    internal class FactionManager_DeclareWar_Patch
    {
        private static bool Prepare()
        {
            return AccessTools.Method(typeof(FactionManager), "DeclareWar") != null;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(FactionManager), "DeclareWar");
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch]
    internal class FactionManager_SetNeutral_Patch
    {
        private static bool Prepare()
        {
            return AccessTools.Method(typeof(FactionManager), "SetNeutral") != null;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(FactionManager), "SetNeutral");
        }

        [HarmonyPrefix]
        private static bool Prefix()
        {
            return false;
        }
    }
}
