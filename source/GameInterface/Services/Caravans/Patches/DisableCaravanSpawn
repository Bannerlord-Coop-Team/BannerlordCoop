using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade; 
        
namespace GameInterface.Services.Caravans.Patches;
protected override void OnSubModuleLoad()
        {
            try
            {
                PatchInitializeCaravanOnCreation();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage("CaravanBlock patch failed: " + ex.Message));
            }
        }

        
        private void PatchInitializeCaravanOnCreation()
        {
            Type nestedType = typeof(CaravanPartyComponent)
                .GetNestedType("InitializationArgs", BindingFlags.NonPublic | BindingFlags.Public);

            if (nestedType == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("CaravanBlock: InitializationArgs type not found"));
                return;
            }

            MethodInfo original = nestedType.GetMethod("InitializeCaravanOnCreation",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (original == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("CaravanBlock: InitializeCaravanOnCreation method not found"));
                return;
            }

            MethodInfo postfix = typeof(CaravanBlockPatch).GetMethod(nameof(CaravanBlockPatch.Postfix));

            _harmony.Patch(original, postfix: new HarmonyMethod(postfix));

            InformationManager.DisplayMessage(new InformationMessage("CaravanBlock: patched InitializeCaravanOnCreation successfully"));
        }
    
    public static class CaravanBlockPatch
    {

        public static void Postfix(MobileParty mobileParty)
        {
            if (mobileParty != null)
            {
                DestroyPartyAction.Apply(null, mobileParty);
            }
        }
    }