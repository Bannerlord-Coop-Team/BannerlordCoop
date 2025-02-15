using Common.Logging;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Registry;
using GameInterface.Services.SiegeStrategies;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.SiegeEvents;


/// <summary>
/// Configures AutoSync for SiegeEvent
/// </summary>
internal class SiegeEventSync : IAutoSync
{
    static readonly ILogger Logger = LogManager.GetLogger<SiegeEventSync>();
    public SiegeEventSync(IAutoSyncBuilder autoSyncBuilder, IAutoRegistryFactory registryFactory)
	{
        // Lifetime
        var ctors = AccessTools.GetDeclaredConstructors(typeof(SiegeEvent));
        registryFactory.TryRegisterType<SiegeEvent>(ctors, RegisterAll);

        // Fields
        autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement)));// WARNING: BesiegedSettlement is a public field, for AutoSync to work you must also add any methods outside declaring class that change its value
		autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegerCamp)));// WARNING: BesiegerCamp is a public field, for AutoSync to work you must also add any methods outside declaring class that change its value
		autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent._isBesiegerDefeated)));

		// Properties
		autoSyncBuilder.AddProperty(AccessTools.Property(typeof(SiegeEvent), nameof(SiegeEvent.SiegeStartTime)));
	}

    void RegisterAll(AutoRegistry<SiegeEvent> registry)
    {
        foreach (var instance in Campaign.Current.SiegeEventManager.SiegeEvents)
        {
            if (registry.RegisterNewObject(instance, out var _) == false) Logger.Error($"Unable to register {nameof(SiegeEvent)}");
        }
    }
}
