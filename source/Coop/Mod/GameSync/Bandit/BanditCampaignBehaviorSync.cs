using CoopFramework;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;

namespace Coop.Mod.GameSync.Bandit
{
    class BanditCampaignBehaviorSync : CoopManaged<BanditCampaignBehaviorSync, BanditsCampaignBehavior>
    {
        static BanditCampaignBehaviorSync()
        {
            // For now, disable all spawning of bandits and new hideouts.
            When(GameLoop)
                .Calls(
                    Method("AddNewHideouts"),
                    Method("CheckForSpawningBanditBoss"),
                    Method("FillANewHideoutWithBandits"),
                    Method("SpawnAPartyInFaction"),                    
                    Method("SpawnBanditOrLooterPartiesAroundAHideoutOrSettlement"),
                    Method("SpawnHideoutsAndBanditsPartiallyOnNewGame"),
                    Method("TryToSpawnHideoutAndBanditHourly"))
                .Skip();

            ApplyStaticPatches();
            AutoWrapAllInstances(i => new BanditCampaignBehaviorSync(i));
        }

        public BanditCampaignBehaviorSync([NotNull] BanditsCampaignBehavior instance) : base(instance)
        {
        }
    }
}
