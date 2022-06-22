using CoopFramework;
using JetBrains.Annotations;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Coop.Mod.GameSync.Bandit
{
    class BanditCampaignBehaviorSync : CoopManaged<BanditCampaignBehaviorSync, BanditsCampaignBehavior>
    {
        static BanditCampaignBehaviorSync()
        {
            // Disable campaign ticks client side
            When(GameLoop & CoopConditions.IsRemoteClient)
                .Calls(
                    Method(nameof(BanditsCampaignBehavior.DailyTick)),
                    Method(nameof(BanditsCampaignBehavior.HourlyTick)),
                    Method(nameof(BanditsCampaignBehavior.WeeklyTick))
                ).Skip();

            ApplyStaticPatches();
            AutoWrapAllInstances(i => new BanditCampaignBehaviorSync(i));
        }

        public BanditCampaignBehaviorSync([NotNull] BanditsCampaignBehavior instance) : base(instance)
        {
        }
    }
}
