using Common.Messaging;
using GameInterface.Utils.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers
{
    public class HeroPropertiesHandler : IHandler
    {
        public HeroPropertiesHandler(IAutoSync autoSync)
        {
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.StaticBodyProperties)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.Weight)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.Build)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.PassedTimeAtHomeSettlement)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.EncyclopediaText)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.IsFemale)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero._battleEquipment)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero._civilianEquipment)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.CaptivityStartTime)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.PreferredUpgradeFormation)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.HeroState)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.IsMinorFactionHero)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.Issue)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.CompanionOf)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.Occupation)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.DeathMark)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.LastKnownClosestSettlement)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.HitPoints)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.DeathDay)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.LastExaminedLogEntryID)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.Clan)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.SupporterOf)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.GovernorOf)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.OwnedAlleys)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.OwnedCaravans)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.PartyBelongedTo)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.PartyBelongedToAsPrisoner)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.StayingInSettlement)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.IsKnownToPlayer)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.HasMet)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.LastMeetingTimeWithPlayer)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.BornSettlement)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.Gold)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.RandomValue)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.BannerItem)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.Father)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.Mother)), GetHeroId);
            autoSync.SyncProperty<Hero>(AccessTools.Property(typeof(Hero), nameof(Hero.Spouse)), GetHeroId);
        }

        public static string GetHeroId(Hero hero)
        {
            return hero.StringId;
        }

        public void Dispose()
        {
        }
    }
}
