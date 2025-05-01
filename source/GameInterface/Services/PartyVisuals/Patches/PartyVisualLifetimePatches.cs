using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyVisuals.Messages;
using HarmonyLib;
using SandBox.View.Map;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Patches
{
    [HarmonyPatch]
    public class PartyVisualLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<PartyVisualLifetimePatches>();

        [HarmonyPatch(typeof(PartyVisual), MethodType.Constructor, typeof(PartyBase))]
        [HarmonyPrefix]
        private static bool CreatePartyVisualPrefix(ref PartyVisual __instance, PartyBase partyBase)
        {
            // Call original if we call this function
            if (CallPolicy.IsOriginalAllowed()) return true;

            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

            var message = new PartyVisualCreated(__instance, partyBase);

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(__instance, message);

            return true;
        }

        [HarmonyPatch(typeof(PartyVisual), nameof(PartyVisual.OnPartyRemoved))]
        [HarmonyPostfix]
        private static void OnMobilePartyDestroyedPostfix(ref PartyVisual __instance)
        {
            if (CallPolicy.IsOriginalAllowed()) return;

            if (GameInterfaceConfig.IsClient)
            {
                Logger.Error("Client destroyed unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(PartyVisual), Environment.StackTrace);
                return;
            }

            var message = new PartyVisualDestroyed(__instance);

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
messageBroker?.Publish(__instance, message);
        }
    }
}
