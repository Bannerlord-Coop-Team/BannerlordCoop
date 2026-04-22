using Common.Logging;
using Common.Messaging;
using GameInterface.Services.CharacterDevelopers.Messages;
using HarmonyLib;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper.PerkSelection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.CharacterDevelopers.Patches
{
    [HarmonyPatch(typeof(CharacterDeveloperHeroItemVM))]
    internal class ApplyChangesPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CharacterDeveloperVM>();

        [HarmonyPatch("ApplyChanges")]
        [HarmonyPrefix]
        public static bool ApplyChanges(ref CharacterDeveloperHeroItemVM __instance)
        {
            // Get data from CharacterDeveloperHeroItemVM
            HeroDeveloper heroDeveloper = __instance.HeroDeveloper;
            PerkSelectionVM perkSelection = __instance.PerkSelection;
            MBBindingList<CharacterAttributeItemVM> attributeSelection = __instance.Attributes;
            MBBindingList<SkillVM> skillSelection = __instance.Skills;

            // Publish message with data
            var message = new ApplyChangesPressed(heroDeveloper, perkSelection, attributeSelection, skillSelection);
            MessageBroker.Instance.Publish(__instance, message);

            // Skip original to override original client saving
            return false;
        }
    }
}
