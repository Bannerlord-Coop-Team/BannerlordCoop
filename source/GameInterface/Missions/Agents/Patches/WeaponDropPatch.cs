using Common.Messaging;
using GameInterface.Missions.Agents.Messages;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Agents.Patches
{
    public class WeaponDropPatch
    {

        [HarmonyPatch(typeof(Agent), "DropItem")]
        public class WeaponDropHandlerPatch
        {
            static void Postfix(ref Agent __instance, EquipmentIndex itemIndex, WeaponClass pickedUpItemType)
            {
                WeaponDropped message = new WeaponDropped(__instance, itemIndex);

                //Commented out as missiles are not functional yet
                MessageBroker.Instance.Publish(__instance, message);
            }
        }
    }
}
