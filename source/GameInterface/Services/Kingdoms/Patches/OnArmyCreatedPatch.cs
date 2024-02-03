using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Armies;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.Kingdoms.Patches
{

    
    [HarmonyPatch(typeof(CampaignEventReceiver))] 
    internal class OnArmyCreatedPatch
    {
        
        [HarmonyPatch("OnArmyCreated")]
        [HarmonyPrefix]
        public static bool OnArmyCreatedPrefix(ref Army army)
        {
            //This method is called when an army is created. It is used to register the army in the registry.

            if(AllowedThread.IsThisThreadAllowed()) { return true; }
            if (PolicyProvider.AllowOriginalCalls) { return true; }

            if (ModInformation.IsClient) { return false; }

            ContainerProvider.TryResolve<IArmyRegistry>(out var registry);
            registry.RegisterNewObject(army, out var newId);


            //registry.TryGetValue(newId, out Army new_Army);
            //army = new_Army;
            
            
            return true;
        }
    }
}
