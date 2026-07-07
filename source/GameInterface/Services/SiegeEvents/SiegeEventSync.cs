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
        // Both are readonly and only written by the SiegeEvent ctor, which the AutoSync transpiler
        // patches, so client shells get them filled without a hand-written message.
        autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegerCamp)));
		autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent._isBesiegerDefeated)));

		// Properties
		autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeEvent), nameof(SiegeEvent.SiegeStartTime)));
	}
}
