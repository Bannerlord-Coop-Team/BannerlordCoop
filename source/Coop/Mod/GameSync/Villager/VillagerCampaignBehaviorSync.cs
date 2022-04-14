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
            // Disable campaign ticks client side
            When(GameLoop & CoopConditions.IsRemoteClient)
                .Calls(
                    Method(nameof(VillagerCampaignBehavior.DailyTick)),
                    Method("HourlyTickParty"),
                    Method("HourlyTickSettlement")
                ).Skip();

            ApplyStaticPatches();
            AutoWrapAllInstances(i => new VillagerCampaignBehaviorSync(i));
        }

        public VillagerCampaignBehaviorSync([NotNull] VillagerCampaignBehavior instance) : base(instance)
        {
        }
    }
}
