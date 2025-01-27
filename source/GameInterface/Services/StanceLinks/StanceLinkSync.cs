using TaleWorlds.CampaignSystem;
using System;
using GameInterface.AutoSync;
using HarmonyLib;

namespace GameInterface.Services.StanceLinks;


/// <summary>
/// Configures AutoSync for <see cref="StanceLink"/>
/// </summary>
internal class StanceLinkSync : IAutoSync
{
	public StanceLinkSync(IAutoSyncBuilder autoSyncBuilder)
	{
		// Fields
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink.BehaviorPriority)));// WARNING: BehaviorPriority is a public field, for AutoSync to work you must also add any methods outside declaring class that change its value
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._casualties1)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._casualties2)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._dailyTributeFrom1To2)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._isAtConstantWar)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._peaceDeclarationDate)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._stanceType)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._successfulRaids1)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._successfulRaids2)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._successfulSieges1)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._successfulSieges2)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._totalTributePaidby1)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._totalTributePaidby2)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._warStartDate)));

		// Properties
		autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.Faction1)));
		autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.Faction2)));
		autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.StanceType)));
	}
}
