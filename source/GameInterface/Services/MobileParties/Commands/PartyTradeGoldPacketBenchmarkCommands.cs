#if DEBUG
using Common;
using Common.Logging;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using System;
using System.Collections.Generic;
using System.Text.Json;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MobileParties.Commands;

/// <summary>
/// Runs an exact wall-clock packet capture for the natural PartyTradeGold campaign traffic.
/// </summary>
internal static class PartyTradeGoldPacketBenchmarkCommands
{
    private const string TargetPacket = "MessagePacket:MobileParty_PartyTradeGold_SetNetworkMessage";
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(3);

    [CommandLineArgumentFunction("party_trade_gold_packet_window_start", "coop.debug.metrics")]
    public static string Start(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Command can only be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.metrics.party_trade_gold_packet_window_start";
        if (Campaign.Current == null)
            return "No campaign is currently loaded.";
        if (!TryGetServices(out var capture, out var timeControl, out var error))
            return error;
        if (timeControl.GetTimeControl() != TimeControlEnum.Pause)
            return "The campaign must be paused before starting the packet window.";

        timeControl.ServerSetTimeControl(TimeControlEnum.Play_2x);
        if (timeControl.GetTimeControl() != TimeControlEnum.Play_2x)
            return "The campaign did not enter Play_2x; the packet window was not started.";
        if (!capture.TryStartCapture(
                TargetPacket,
                Window,
                () => GameThread.RunSafe(
                    () => timeControl.ServerSetTimeControl(TimeControlEnum.Pause),
                    context: nameof(PartyTradeGoldPacketBenchmarkCommands)),
                out var snapshot,
                out error))
        {
            timeControl.ServerSetTimeControl(TimeControlEnum.Pause);
            return error;
        }

        return Format(snapshot, timeControl.GetTimeControl());
    }

    [CommandLineArgumentFunction("party_trade_gold_packet_window_status", "coop.debug.metrics")]
    public static string Status(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Command can only be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.metrics.party_trade_gold_packet_window_status";
        if (!TryGetServices(out var capture, out var timeControl, out var error))
            return error;
        if (!capture.TryGetCapture(out var snapshot, out error))
            return error;

        return Format(snapshot, timeControl.GetTimeControl());
    }

    [CommandLineArgumentFunction("party_trade_gold_packet_window_restore", "coop.debug.metrics")]
    public static string Restore(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Command can only be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.metrics.party_trade_gold_packet_window_restore";
        if (!TryGetServices(out var capture, out var timeControl, out var error))
            return error;

        if (!capture.TryGetCapture(out var snapshot, out error))
            return error;
        if (snapshot.State == "running" && !capture.TryCancelCapture(out snapshot, out error))
            return error;

        timeControl.ServerSetTimeControl(TimeControlEnum.Pause);
        return Format(snapshot, timeControl.GetTimeControl());
    }

    [CommandLineArgumentFunction("party_trade_gold_packet_window_verify", "coop.debug.metrics")]
    public static string Verify(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Command can only be run on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.metrics.party_trade_gold_packet_window_verify";
        if (!TryGetServices(out var capture, out var timeControl, out var error))
            return error;
        if (!capture.TryGetCapture(out var snapshot, out error))
            return error;
        if (snapshot.State != "completed" || snapshot.Cancelled ||
            snapshot.WindowDurationMilliseconds != (long)Window.TotalMilliseconds ||
            timeControl.GetTimeControl() != TimeControlEnum.Pause)
        {
            return "Packet window is not complete and restored. " + Format(snapshot, timeControl.GetTimeControl());
        }

        return Format(snapshot, timeControl.GetTimeControl(), verified: true);
    }

    private static bool TryGetServices(
        out IPacketProfileCapture capture,
        out ITimeControlInterface timeControl,
        out string error)
    {
        capture = null;
        timeControl = null;
        error = null;
        if (!ContainerProvider.TryResolve(out capture))
        {
            error = $"Unable to resolve {nameof(IPacketProfileCapture)}.";
            return false;
        }
        if (!ContainerProvider.TryResolve(out timeControl))
        {
            error = $"Unable to resolve {nameof(ITimeControlInterface)}.";
            return false;
        }

        return true;
    }

    private static string Format(
        PacketProfileCaptureSnapshot snapshot,
        TimeControlEnum timeMode,
        bool verified = false)
    {
        string output = $"captureId={snapshot.CaptureId}|state={snapshot.State}|" +
            $"packet={snapshot.PacketName}|packets={snapshot.PacketsSent}|bytes={snapshot.BytesSent}|" +
            $"windowMs={snapshot.WindowDurationMilliseconds}|elapsedMs={snapshot.ElapsedMilliseconds}|" +
            $"timeMode={timeMode}|verified={verified.ToString().ToLowerInvariant()}";
        return output + Environment.NewLine + "LIVE_TEST_JSON=" + JsonSerializer.Serialize(new
        {
            captureId = snapshot.CaptureId,
            state = snapshot.State,
            packet = snapshot.PacketName,
            packets = snapshot.PacketsSent,
            bytes = snapshot.BytesSent,
            windowDurationMilliseconds = snapshot.WindowDurationMilliseconds,
            elapsedMilliseconds = snapshot.ElapsedMilliseconds,
            startedUtc = snapshot.StartedUtc,
            expectedCompletedUtc = snapshot.ExpectedCompletedUtc,
            completedUtc = snapshot.CompletedUtc,
            cancelled = snapshot.Cancelled,
            timeMode = timeMode.ToString(),
            verified,
        });
    }
}
#endif
