using CoopFramework;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors.VillageBehaviors;

namespace Coop.Mod.GameSync.Bandit
{
    class VillagerCampaignBehaviorSync : CoopManaged<VillagerCampaignBehaviorSync, VillagerCampaignBehavior>
    {
        static VillagerCampaignBehaviorSync()
        {
            // For now, disable all spawning
            When(GameLoop)
                .Calls(
                    Method("CreateVillagerParty"))
                .Skip();

            ApplyStaticPatches();
            AutoWrapAllInstances(i => new VillagerCampaignBehaviorSync(i));
        }

        public VillagerCampaignBehaviorSync([NotNull] VillagerCampaignBehavior instance) : base(instance)
        {
        }
    }
}
