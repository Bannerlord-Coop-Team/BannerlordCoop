using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using GameInterface.Registry.Auto;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.Heroes
{
    internal class HeroSync : IDynamicSync
    {
        private IEnumerable<MethodInfo> externalMethods => new MethodInfo[]
        {
            AccessTools.Method(typeof(HeroDeveloper), "CheckLevel"),
            AccessTools.Method(typeof(HeroDeveloper), "ClearHeroLevel"),
            AccessTools.Method(typeof(MakePregnantAction), nameof(MakePregnantAction.ApplyInternal)),
            AccessTools.Method(typeof(PregnancyCampaignBehavior), "CheckOffspringsToDeliver", new Type[] { typeof(Hero) }),
            AccessTools.Method(typeof(PregnancyCampaignBehavior), "CheckOffspringsToDeliver", new Type[] { typeof(PregnancyCampaignBehavior.Pregnancy) }),
            AccessTools.Method(typeof(HeroCreator), nameof(HeroCreator.CreateRelativeNotableHero)),
            AccessTools.Method(typeof(HeroCreator), nameof(HeroCreator.DeliverOffSpring)),
        };

        public HeroSync(DynamicSyncRegistry autoSyncBuilder)
        {
            foreach (var method in externalMethods)
            {
                //ISSUES WITH THIS
                //autoSyncBuilder.AddTargetMethod(typeof(Hero), method);
            }

            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.StaticBodyProperties)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.Weight)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.Build)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.PassedTimeAtHomeSettlement)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.EncyclopediaText)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero.IsFemale)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero._battleEquipment)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero._civilianEquipment)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hero), nameof(Hero._stealthEquipment)));
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


            // TODO add all fields
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Hero), nameof(Hero._health)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Hero), nameof(Hero.Culture)));
        }
    }
}
