using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.CraftingService.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Army;

namespace GameInterface.Services.CraftingService.Patches
{
    [HarmonyPatch]
    public class CraftingLifetimePatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingLifetimePatches>();

        [HarmonyPatch(typeof(Crafting), MethodType.Constructor, typeof(CraftingTemplate), typeof(BasicCultureObject), typeof(TextObject))]
        [HarmonyPrefix]
        private static bool CreateCraftingPrefix(ref Crafting __instance, CraftingTemplate craftingTemplate, BasicCultureObject culture, TextObject name)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(Crafting), Environment.StackTrace);

                return true;
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                objectManager.AddNewObject(__instance, out var newId);

                var data = new CraftingCreatedData(newId, craftingTemplate.StringId, culture.StringId, name.Value);
                var message = new CraftingCreated(data);

                MessageBroker.Instance.Publish(null, message);
            }

            return true;
        }
    }
}
