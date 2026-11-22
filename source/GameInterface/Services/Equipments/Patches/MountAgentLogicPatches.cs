using Common;
using Common.Messaging;
using GameInterface.Services.Equipments.Messages;
using HarmonyLib;
using Helpers;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Equipments.Patches;

[HarmonyPatch(typeof(MountAgentLogic))]
internal class MountAgentLogicPatches
{
    // Rolls the horse loss like vanilla, but leaves the equipment and roster writes to the server so they replicate
    [HarmonyPatch(nameof(MountAgentLogic.OnAgentRemoved))]
    [HarmonyPrefix]
    private static bool OnAgentRemovedPrefix(MountAgentLogic __instance, Agent affectedAgent, AgentState agentState)
    {
        if (ModInformation.IsServer) return true;

        if (!affectedAgent.IsMount || agentState != AgentState.Killed || __instance._mainHeroMountAgent != affectedAgent) return false;

        bool usesCivilianEquipment = Mission.Current.DoesMissionRequireCivilianEquipment;
        Equipment equipment = usesCivilianEquipment ? Hero.MainHero.CivilianEquipment : Hero.MainHero.BattleEquipment;

        float roll = MBRandom.RandomFloat;
        float lossChance = 0.05f;
        float lameChance = 0.2f;
        if (Hero.MainHero.GetPerkValue(DefaultPerks.Riding.WellStraped) &&
            PerkHelper.TryGetPerkEffectBonus(DefaultPerks.Riding.WellStraped, isPrimary: true, out var bonus, affectedAgent.CurrentBattleEnvironment))
        {
            lossChance += lossChance * bonus;
            lameChance += lameChance * bonus;
        }

        EquipmentElement horse = equipment[EquipmentIndex.Horse];
        bool horseLost = roll < lossChance || horse.ItemModifier?.StringId == "lame_horse";
        bool horseLamed = !horseLost && roll < lameChance;
        if (!horseLost && !horseLamed) return false;

        MessageBroker.Instance.Publish(__instance, new MainHeroMountKilled(Hero.MainHero, usesCivilianEquipment, horseLost));

        if (horseLost)
        {
            MBInformationManager.AddQuickInformation(new TextObject("{=nZhPS83J}You lost your horse."));
        }
        else
        {
            var lamedHorse = new EquipmentElement(horse.Item, MBObjectManager.Instance.GetObject<ItemModifier>("lame_horse"));
            TextObject textObject = new TextObject("{=a6hwSEAK}Your horse is turned into a {MODIFIED_NAME}, since it got seriously injured.");
            textObject.SetTextVariable("MODIFIED_NAME", lamedHorse.GetModifiedItemName());
            MBInformationManager.AddQuickInformation(textObject);
        }

        return false;
    }
}
