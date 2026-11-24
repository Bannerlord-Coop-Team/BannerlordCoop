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

        float bonus = 0f;
        if (Hero.MainHero.GetPerkValue(DefaultPerks.Riding.WellStraped) &&
            PerkHelper.TryGetPerkEffectBonus(DefaultPerks.Riding.WellStraped, isPrimary: true, out var perkBonus, affectedAgent.CurrentBattleEnvironment))
            bonus = perkBonus;

        EquipmentElement horse = equipment[EquipmentIndex.Horse];
        if (RollMountLoss(MBRandom.RandomFloat, bonus, horse) is not MountLoss loss) return false;

        MessageBroker.Instance.Publish(__instance, new MainHeroMountKilled(Hero.MainHero, usesCivilianEquipment, loss));

        if (loss == MountLoss.Lost)
        {
            MBInformationManager.AddQuickInformation(new TextObject("{=nZhPS83J}You lost your horse."));
        }
        else
        {
            var lamedHorse = new EquipmentElement(horse.Item, MBObjectManager.Instance.GetObject<ItemModifier>(MainHeroMountKilled.LameHorseModifierId));
            TextObject textObject = new TextObject("{=a6hwSEAK}Your horse is turned into a {MODIFIED_NAME}, since it got seriously injured.");
            textObject.SetTextVariable("MODIFIED_NAME", lamedHorse.GetModifiedItemName());
            MBInformationManager.AddQuickInformation(textObject);
        }

        return false;
    }

    // Vanilla's odds, raised by the WellStraped bonus, and vanilla always loses a horse that is already lame
    private static MountLoss? RollMountLoss(float roll, float bonus, EquipmentElement horse)
    {
        float lossChance = 0.05f + (0.05f * bonus);
        float lameChance = 0.2f + (0.2f * bonus);

        if (roll < lossChance || horse.ItemModifier?.StringId == MainHeroMountKilled.LameHorseModifierId) return MountLoss.Lost;
        if (roll < lameChance) return MountLoss.Lamed;
        return null;
    }
}
