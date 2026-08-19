using Common.Logging;
using GameInterface.Configuration;
using Serilog;
using System;

namespace Missions.Agents.Handlers;

public interface IMovementNetworkSettings
{
    int OutgoingBytesPerSecond { get; }
    int IncomingBytesPerSecond { get; }
    double OutgoingMiBPerSecond { get; }
    double IncomingMiBPerSecond { get; }
}

/// <summary>Validates local movement bandwidth settings and exposes byte-rate limits.</summary>
public sealed class MovementNetworkSettings : IMovementNetworkSettings
{
    private static readonly ILogger Logger = LogManager.GetLogger<MovementNetworkSettings>();

    public const int BytesPerMiB = 1024 * 1024;
    public const double DefaultMiBPerSecond = 1d;
    private const double MaximumMiBPerSecond = 1024d;

    public int OutgoingBytesPerSecond { get; }
    public int IncomingBytesPerSecond { get; }
    public double OutgoingMiBPerSecond { get; }
    public double IncomingMiBPerSecond { get; }

    public MovementNetworkSettings(IModConfig modConfig)
    {
        NetworkConfigData config = modConfig?.Data?.Network;
        OutgoingMiBPerSecond = Validate(
            config?.MovementOutgoingMiBPerSecond,
            "movementOutgoingMiBPerSecond");
        IncomingMiBPerSecond = Validate(
            config?.MovementIncomingMiBPerSecond,
            "movementIncomingMiBPerSecond");
        OutgoingBytesPerSecond = ToBytes(OutgoingMiBPerSecond);
        IncomingBytesPerSecond = ToBytes(IncomingMiBPerSecond);
    }

    internal MovementNetworkSettings(double outgoingMiBPerSecond, double incomingMiBPerSecond)
    {
        OutgoingMiBPerSecond = Validate(outgoingMiBPerSecond, nameof(outgoingMiBPerSecond));
        IncomingMiBPerSecond = Validate(incomingMiBPerSecond, nameof(incomingMiBPerSecond));
        OutgoingBytesPerSecond = ToBytes(OutgoingMiBPerSecond);
        IncomingBytesPerSecond = ToBytes(IncomingMiBPerSecond);
    }

    private double Validate(double? configured, string name)
    {
        if (configured.HasValue &&
            !double.IsNaN(configured.Value) &&
            !double.IsInfinity(configured.Value) &&
            configured.Value > 0d &&
            configured.Value <= MaximumMiBPerSecond)
        {
            return configured.Value;
        }

        if (configured.HasValue)
        {
            Logger.Warning(
                "mod-config.json: network value for '{Setting}' must be greater than zero and no more than {Maximum}; using {Default}",
                name,
                MaximumMiBPerSecond,
                DefaultMiBPerSecond);
        }
        return DefaultMiBPerSecond;
    }

    private static int ToBytes(double mebibytesPerSecond) =>
        Math.Max(
            1,
            checked((int)Math.Round(mebibytesPerSecond * BytesPerMiB)));
}
