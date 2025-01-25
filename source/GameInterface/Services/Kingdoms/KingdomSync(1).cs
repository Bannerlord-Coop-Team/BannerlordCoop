using TaleWorlds.CampaignSystem;
using System;
using TaleWorlds.CampaignSystem.Settlements;
using GameInterface.AutoSync;
using HarmonyLib;

namespace GameInterface.Services.Kingdoms;


/// <summary>
/// Configures AutoSync for <see cref="Kingdom"/>
/// </summary>
internal class KingdomSync : IAutoSync
{
	public KingdomSync(IAutoSyncBuilder autoSyncBuilder)
	{
		// Fields
		autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom.PoliticalStagnation)));// WARNING: PoliticalStagnation is a public field, for AutoSync to work you must also add any methods outside declaring class that change its value
		autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._aggressiveness)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._isEliminated)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._kingdomBudgetWallet)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._kingdomMidSettlement)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._rulingClan)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._tributeWallet)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(Kingdom), nameof(Kingdom._distanceToClosestNonAllyFortificationCacheDirty)));

		// Properties
	}
}
