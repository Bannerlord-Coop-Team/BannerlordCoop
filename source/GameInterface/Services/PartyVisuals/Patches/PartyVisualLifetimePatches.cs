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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(PartyVisual), Environment.StackTrace);
                return true;
            }

            var message = new PartyVisualCreated(__instance, partyBase);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }

        [HarmonyPatch(typeof(PartyVisual), nameof(PartyVisual.OnPartyRemoved))]
        [HarmonyPostfix]
        private static void OnMobilePartyDestroyedPostfix(ref PartyVisual __instance)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client destroyed unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(PartyVisual), Environment.StackTrace);
                return;
            }

            var message = new PartyVisualDestroyed(__instance);

            MessageBroker.Instance.Publish(__instance, message);
        }
    }
}
