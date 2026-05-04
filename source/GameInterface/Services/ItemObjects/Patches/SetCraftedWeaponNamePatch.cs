using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ItemObjects.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.ItemObjects.Patches
{
    [HarmonyPatch(typeof(ItemObject))]
    internal class SetCraftedWeaponNamePatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ItemObject>();

        [HarmonyPatch("SetCraftedWeaponName")]
        [HarmonyPrefix]
        public static bool SetCraftedWeaponName(ref ItemObject __instance, TextObject weaponName)
        {
            var message = new CraftedWeaponNameSet(__instance, weaponName);
            MessageBroker.Instance.Publish(__instance, message);

            return false;
        }
    }
}