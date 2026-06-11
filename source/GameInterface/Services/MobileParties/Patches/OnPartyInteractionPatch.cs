using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Skips <see cref="MobileParty.OnPartyInteraction"/> when the party has no members.
/// An empty <see cref="MobileParty.MemberRoster"/> causes the original method to
/// operate on a party that has effectively been emptied/destroyed.
/// </summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.OnPartyInteraction))]
internal class OnPartyInteractionPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<OnPartyInteractionPatch>();

    [HarmonyPrefix]
    private static bool Prefix(MobileParty __instance)
    {
        if (__instance?.MemberRoster == null || __instance.MemberRoster.Count == 0)
        {
            Logger.Verbose("Skipping {Method} for party '{Party}' because its MemberRoster is empty",
                nameof(MobileParty.OnPartyInteraction), __instance?.StringId ?? "<null>");
            return false;
        }

        return true;
    }
}
