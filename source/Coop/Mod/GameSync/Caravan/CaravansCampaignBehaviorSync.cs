using CoopFramework;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;

namespace Coop.Mod.GameSync.Bandit
{
    class CaravansCampaignBehaviorSync : CoopManaged<CaravansCampaignBehaviorSync, CaravansCampaignBehavior>
    {
        static CaravansCampaignBehaviorSync()
        {
            // For now, disable all spawning
            When(GameLoop)
                .Calls(
                    Method(nameof(CaravansCampaignBehavior.SpawnCaravan)))
                .Skip();

            // On client, disable caravan decision making
            When(GameLoop & CoopConditions.IsRemoteClient)
                .Calls(
                    Method(nameof(CaravansCampaignBehavior.HourlyTickParty)))
                .Skip();

            ApplyStaticPatches();
            AutoWrapAllInstances(i => new CaravansCampaignBehaviorSync(i));
        }

        public CaravansCampaignBehaviorSync([NotNull] CaravansCampaignBehavior instance) : base(instance)
        {
        }
    }
}
