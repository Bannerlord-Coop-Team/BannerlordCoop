using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Messages.RomanceFlow;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Romance = TaleWorlds.CampaignSystem.Romance;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(ChangeRomanticStateAction))]
internal class RomanceActionPatches
{
    [HarmonyPatch(nameof(ChangeRomanticStateAction.Apply))]
    [HarmonyPrefix]
    private static bool ApplyPrefix(Hero person1, Hero person2, Romance.RomanceLevelEnum toWhat)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (person1.IsControlledByThisInstance() || person2.IsControlledByThisInstance())
        {
            var state = Romance.GetRomanticState(person1, person2);
            MessageBroker.Instance.Publish(
                person1,
                new RomanticStateChangeRequested(
                    person1,
                    person2,
                    toWhat,
                    state?.ProgressToNextLevel ?? 0,
                    state?.LastVisit ?? 0f,
                    state?.ScoreFromPersuasion ?? 0f));

            using (new AllowedThread())
            {
                ChangeRomanticStateAction.Apply(person1, person2, toWhat);
            }
        }

        return false;
    }

    [HarmonyPatch(nameof(ChangeRomanticStateAction.Apply))]
    [HarmonyPostfix]
    private static void ApplyPostfix()
    {
        if (!ModInformation.IsServer) return;

        MessageBroker.Instance.Publish(null, new RomanceStatesChanged());
    }
}

[HarmonyPatch(typeof(MarriageAction))]
internal class MarriageActionPatches
{
    [HarmonyPatch(nameof(MarriageAction.Apply))]
    [HarmonyPrefix]
    private static bool ApplyPrefix()
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
