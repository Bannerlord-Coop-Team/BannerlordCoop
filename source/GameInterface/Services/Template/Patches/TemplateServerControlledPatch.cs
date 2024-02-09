using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Template.Messages;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Template.Patches;

/// <summary>
/// TODO fill me out and uncomment HarmonyPatch attributes
/// </summary>
//[HarmonyPatch(typeof(Campaign))]
class TemplateServerControlledPatch
{
    // See https://harmony.pardeike.net/articles/intro.html on how to use harmony patches
    //[HarmonyPatch("TimeControlMode")]
    //[HarmonyPatch(MethodType.Setter)]
    //[HarmonyPrefix]
    private static bool Prefix(ref Campaign __instance)
    {
        // Allows original method call when called by OverrideTemplateFn 
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Skip method if called from client and allow origin
        if (ModInformation.IsClient) return false;

        // Publishing a message to all internal software is done using the message broker
        // This type of message should be IEvent since it is a reaction to something
        // Normally sent to a handler in Coop.Core
        MessageBroker.Instance.Publish(__instance, new TemplateEventMessage());

        // Returning true allows original on the server to run
        return true;
    }

    public static void OverrideTemplateFn()
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            // Allowed thread will call the original function rather than skip or do patch functionality
            // See if (AllowedThread.IsThisThreadAllowed()) return true; in the method above
            using (new AllowedThread())
            {
                // Do something with the patched instance here
            }
        }, blocking: true);


        // This is equivalant to the using statement above
        // Only one version is needed
        GameLoopRunner.RunOnMainThread(() =>
        {
            AllowedThread.AllowThisThread();
            // Do something with the patched instance here
            AllowedThread.RevokeThisThread();
        }, blocking: true);
    }
}
