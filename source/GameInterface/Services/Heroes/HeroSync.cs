using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes
{
    internal class HeroSync : IAutoSync
    {
        public HeroSync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.StaticBodyProperties)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.Weight)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.Build)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.PassedTimeAtHomeSettlement)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.EncyclopediaText)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.IsFemale)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero._battleEquipment)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero._civilianEquipment)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.CaptivityStartTime)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.PreferredUpgradeFormation)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.HeroState)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.IsMinorFactionHero)));
            //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.Issue)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.CompanionOf)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.Occupation)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.DeathMark)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.DeathMarkKillerHero)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.LastKnownClosestSettlement)));
            //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.HitPoints)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.DeathDay)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.LastExaminedLogEntryID)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.Clan)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.SupporterOf)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.GovernorOf)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.PartyBelongedTo)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.PartyBelongedToAsPrisoner)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.StayingInSettlement)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.IsKnownToPlayer)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.HasMet)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.LastMeetingTimeWithPlayer)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.BornSettlement)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.Gold)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.RandomValue)));
            //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.BannerItem)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.Father)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.Mother)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.Spouse)));

            autoSyncBuilder.AddField(AccessTools.Field(typeof(Hero), nameof(Hero._health)));
        }
    }
}
