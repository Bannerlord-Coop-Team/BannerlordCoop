using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.CommentBehaviors;
using TaleWorlds.CampaignSystem.LogEntries;
using Romance = TaleWorlds.CampaignSystem.Romance;

namespace GameInterface.Services.UI.LogEntries.Patches;

/// <summary>Keeps vanilla's romance log rule working for retired heroes, who lose their clan when it closes but keep their romance states.</summary>
[HarmonyPatch(typeof(CommentOnChangeRomanticStateBehavior))]
internal class CommentOnChangeRomanticStatePatches
{
    // Same condition as vanilla, which reads Clan.Leader without a null check.
    [HarmonyPatch(nameof(CommentOnChangeRomanticStateBehavior.OnRomanticStateChanged))]
    [HarmonyPrefix]
    public static bool OnRomanticStateChangedPrefix(Hero hero1, Hero hero2, Romance.RomanceLevelEnum level)
    {
        if (hero1 == Hero.MainHero || hero2 == Hero.MainHero || hero1.Clan?.Leader == hero1 || hero2.Clan?.Leader == hero2)
        {
            LogEntry.AddLogEntry(new ChangeRomanticStateLogEntry(hero1, hero2, level));
        }

        return false;
    }
}
