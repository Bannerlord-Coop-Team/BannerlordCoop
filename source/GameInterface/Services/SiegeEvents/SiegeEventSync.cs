using Common.Logging;
using GameInterface.AutoSync;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents;


/// <summary>
/// Configures AutoSync for SiegeEvent
/// </summary>
internal class SiegeEventSync : IAutoSync
{
    static readonly ILogger Logger = LogManager.GetLogger<SiegeEventSync>();
    public SiegeEventSync(AutoSyncRegistry autoSyncBuilder)
	{
        // Fields
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement)));// WARNING: BesiegedSettlement is a public field, for AutoSync to work you must also add any methods outside declaring class that change its value
		//autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegerCamp)));// WARNING: BesiegerCamp is a public field, for AutoSync to work you must also add any methods outside declaring class that change its value
		autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent._isBesiegerDefeated)));

		// Properties
		autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeEvent), nameof(SiegeEvent.SiegeStartTime)));
	}
}
