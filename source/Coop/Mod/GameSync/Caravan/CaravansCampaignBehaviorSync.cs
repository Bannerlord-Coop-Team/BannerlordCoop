using CoopFramework;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;

namespace Coop.Mod.GameSync.Bandit
{
    class CaravansCampaignBehaviorSync : CoopManaged<CaravansCampaignBehaviorSync, CaravansCampaignBehavior>
    {
        static CaravansCampaignBehaviorSync()
        {
            // Disable campaign ticks client side
            When(GameLoop & CoopConditions.IsRemoteClient)
                .Calls(
                    Method(nameof(CaravansCampaignBehavior.DailyTick)),
                    Method("DailyTickHero"),
                    Method("HourlyTickParty")
                ).Skip();

            ApplyStaticPatches();
            AutoWrapAllInstances(i => new CaravansCampaignBehaviorSync(i));
        }

        public CaravansCampaignBehaviorSync([NotNull] CaravansCampaignBehavior instance) : base(instance)
        {
        }
    }
}
