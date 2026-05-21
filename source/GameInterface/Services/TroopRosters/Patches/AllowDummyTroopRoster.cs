//using Common.Util;
//using HarmonyLib;
//using System.Runtime.CompilerServices;
//using TaleWorlds.CampaignSystem.Roster;

//namespace GameInterface.Services.TroopRosters.Patches;


///// <summary>
///// Dummy troop roster is only used for caching, this can safely be allowed
///// </summary>
//[HarmonyPatch(typeof(TroopRoster))]
//internal class AllowDummyTroopRoster
//{
//    public static bool IsDummyRoster(TroopRoster roster) => DummyRosters.TryGetValue(roster, out var _);
//    public static bool RemoveDummyRoster(TroopRoster roster) => DummyRosters.Remove(roster);

//    private static ConditionalWeakTable<TroopRoster, RosterExtendedData> DummyRosters = new();

//    class RosterExtendedData
//    {
//        public bool IsDummyRoster;
//    }

//    [HarmonyPatch(nameof(TroopRoster.CreateDummyTroopRoster))]
//    [HarmonyPrefix]
//    private static void PrefixCreateDummyTroopRoster()
//    {
//        AllowedThread.AllowThisThread();
//    }

//    [HarmonyPatch(nameof(TroopRoster.CreateDummyTroopRoster))]
//    [HarmonyPostfix]
//    private static void PostfixCreateDummyTroopRoster(ref TroopRoster __result)
//    {
//        DummyRosters.GetOrCreateValue(__result);
//        AllowedThread.RevokeThisThread();
//    }
//}
