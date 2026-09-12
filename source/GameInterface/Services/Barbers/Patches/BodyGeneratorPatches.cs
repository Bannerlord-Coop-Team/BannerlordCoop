using Common;
using Common.Messaging;
using GameInterface.Services.Barbers.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Barbers.Patches;

[HarmonyPatch(typeof(BodyGenerator))]
internal class BodyGeneratorPatches
{
    /// <summary>
    /// Postfix instead of replacing with prefix. Character creation needs to behave as normal when
    /// clients are joining a game as their hero/character isn't registered yet. Server overrides the changes after
    /// for any character changes that aren't done in the character creation screen.
    /// </summary>
    [HarmonyPatch(nameof(BodyGenerator.SaveCurrentCharacter))]
    [HarmonyPostfix]
    public static void SaveCurrentCharacterPostfix(BodyGenerator __instance)
    {
        if (ModInformation.IsServer) return;

        var message = new SaveCurrentCharacter(Hero.MainHero.CharacterObject, __instance.CurrentBodyProperties, __instance.Race, __instance.IsFemale);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
