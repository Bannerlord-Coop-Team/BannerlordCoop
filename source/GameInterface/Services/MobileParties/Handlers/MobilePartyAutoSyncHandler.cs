using Common.Messaging;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Utils.AutoSync;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers;
public class MobilePartyAutoSyncHandler : IHandler
{

    public MobilePartyAutoSyncHandler(IAutoSync autoSync)
    {
        var properties = new HashSet<PropertyInfo>()
        {
           AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Army)),
          //  AccessTools.Property(typeof(MobileParty), nameof(MobileParty.CustomName)),
       /*     AccessTools.Property(typeof(MobileParty), nameof(MobileParty.LastVisitedSettlement)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Aggressiveness)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.ArmyPositionAdder)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Objective)),

            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Ai)), 
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Party)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsActive)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.ThinkParamsCache)),

            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.ShortTermBehavior)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsPartyTradeActive)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.PartyTradeGold)), 
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.PartyTradeTaxGold)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.StationaryStartTime)),

            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.VersionNo)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.ShouldJoinPlayerBattles)), 
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsDisbanding)), 
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.CurrentSettlement)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.AttachedTo)), 
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.BesiegerCamp)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Scout)), 

            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Engineer)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Quartermaster)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Surgeon)), 
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.ActualClan)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.RecentEventsMorale)),

            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.EventPositionAdder)),
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsInspected)), 
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.MapEventSide)), 
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.PartyComponent)), 
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsLordParty)), 
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsVillager)), 


            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsCaravan)), 
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsGarrison)), 
            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsCustomParty)), 

            AccessTools.Property(typeof(MobileParty), nameof(MobileParty.IsBandit)), */
    };


        foreach (var property in properties)
        {
            autoSync.SyncProperty<MobileParty>(property, GetMobilePartyId);
        }
    }

    public static string GetMobilePartyId(MobileParty mobileParty) {

        return mobileParty.StringId;
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
