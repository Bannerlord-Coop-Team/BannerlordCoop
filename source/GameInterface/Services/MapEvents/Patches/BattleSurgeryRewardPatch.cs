using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch(typeof(SkillLevelingManager), nameof(SkillLevelingManager.OnSurgeryApplied))]
internal class BattleSurgeryRewardPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(MobileParty party, bool surgerySuccess, int troopTier)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || ModInformation.IsServer) return true;
        if (!BattleSpawnConfig.Enabled || !BattleSpawnGate.IsCoopBattleActive) return true;

        // Vanilla has already checked surgery use and the active surgeon; only the server may award XP.
        if (party?.MapEvent != null)
            MessageBroker.Instance.Publish(party, new BattleSurgeryReward(party, surgerySuccess, troopTier));
        return false;
    }
}
