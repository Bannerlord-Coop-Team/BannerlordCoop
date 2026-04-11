using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Template.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Template.Patches;

/// <summary>
/// This template demonstrates how to create a Harmony patch that controls server-side values.
/// Patches modify game behavior at runtime and are useful for enforcing server-side rules.
/// Uncomment the HarmonyPatch attributes when applying this patch.
/// </summary>
//[HarmonyPatch(typeof(MobileParty))]
class TemplateServerControlledPatch
{
    private static ILogger Logger { get; } = LogManager.GetLogger<TemplateServerControlledPatch>();

    /// <summary>
    /// Prefix method that intercepts and modifies the behavior of the target method before it runs.
    /// This ensures that only the server can modify certain values.
    /// </summary>
    /// <param name="__instance">The instance of the Campaign class being modified.</param>
    /// <param name="value">The new value being assigned.</param>
    /// <remarks>
    /// See https://harmony.pardeike.net/articles/intro.html on how to use harmony patches
    /// </remarks>
    //[HarmonyPatch(nameof(MobileParty.Scout))]
    //[HarmonyPatch(MethodType.Setter)]
    //[HarmonyPrefix]
    private static void Prefix(ref MobileParty __instance, float value)
    {
        // Allow the original method call if it is triggered by OverrideTemplateFn
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        // Prevent clients from modifying server-controlled values
        if (ModInformation.IsClient)
        {
            Logger.Error("Client attempted to change a server-controlled value: {name}", nameof(Campaign.AverageWage));
            return;
        }

        // Publish an event message when this value is modified.
        // This allows other parts of the system to react accordingly.
        // Use an IEvent message type because this is a reaction, not a command.
        MessageBroker.Instance.Publish(__instance, new TemplateEventMessage(__instance, value));
    }
}