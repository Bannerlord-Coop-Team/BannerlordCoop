using Common;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Logging;

internal interface ITroopRosterLogger
{
    void Debug(TroopRoster troopRoster, string messageTemplate, params object[] propertyValues);
    void Debug(string troopRosterId, string messageTemplate, params object[] propertyValues);
}

/// <summary>
/// Diagnostic logging for troop roster syncing. Debug output is gated behind
/// <see cref="TroopRosterConfig.Debug"/> so it can be toggled without touching call sites.
/// </summary>
internal sealed class TroopRosterLogger : ITroopRosterLogger
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterLogger>();

    private readonly IObjectManager objectManager;

    public TroopRosterLogger(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public void Debug(TroopRoster troopRoster, string messageTemplate, params object[] propertyValues)
    {
        if (!TroopRosterConfig.Debug) return;

        if (!objectManager.TryGetIdWithLogging(troopRoster, out string troopRosterId)) return;

        Debug(troopRosterId, messageTemplate, propertyValues);
    }

    public void Debug(string troopRosterId, string messageTemplate, params object[] propertyValues)
    {
        if (!TroopRosterConfig.Debug) return;

        var values = BuildPropertyValues(troopRosterId, propertyValues);

        Logger.Debug(
            "[{InstanceType}][TroopRoster:{TroopRosterId}] " + messageTemplate,
            values);
    }

    private static object[] BuildPropertyValues(string troopRosterId, object[] propertyValues)
    {
        propertyValues ??= Array.Empty<object>();

        var values = new object[propertyValues.Length + 2];
        values[0] = ModInformation.IsServer ? "Server" : "Client";
        values[1] = troopRosterId;

        Array.Copy(propertyValues, 0, values, 2, propertyValues.Length);

        return values;
    }
}
