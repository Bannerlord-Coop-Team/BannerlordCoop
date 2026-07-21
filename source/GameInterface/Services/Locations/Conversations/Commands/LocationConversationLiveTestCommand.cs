#if DEBUG
using Common;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Locations.Conversations.Commands;

internal static class LocationConversationLiveTestProbe
{
    private static int lastAllowedGeneration = -1;
    private static int lastDeniedGeneration = -1;
    private static int hasApprovedEngagement;

    public static bool Enabled { get; private set; }
    public static int LastAllowedGeneration => Volatile.Read(ref lastAllowedGeneration);
    public static int LastDeniedGeneration => Volatile.Read(ref lastDeniedGeneration);
    public static bool HasApprovedEngagement => Volatile.Read(ref hasApprovedEngagement) == 1;

    public static void Enable()
    {
        Enabled = true;
        ResetResponses();
    }

    public static void Disable()
    {
        Enabled = false;
        ResetResponses();
    }

    public static void RecordAllowed(int generation)
    {
        Volatile.Write(ref lastAllowedGeneration, generation);
        Volatile.Write(ref hasApprovedEngagement, 1);
    }

    public static void RecordDenied(int generation) => Volatile.Write(ref lastDeniedGeneration, generation);
    public static void RecordEnded() => Volatile.Write(ref hasApprovedEngagement, 0);

    private static void ResetResponses()
    {
        Volatile.Write(ref lastAllowedGeneration, -1);
        Volatile.Write(ref lastDeniedGeneration, -1);
        Volatile.Write(ref hasApprovedEngagement, 0);
    }
}

/// <summary>
/// DEBUG-only commands used by the automated live test for reciprocal location conversations.
/// </summary>
public static class LocationConversationLiveTestCommand
{
    private const string SyntheticLocationId = "Location_live_test";

    [CommandLineArgumentFunction("players", "coop.debug.location_conversation")]
    public static string Players(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.players";
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return $"Unable to resolve {nameof(IPlayerManager)}";

        var players = playerManager.Players
            .OrderBy(player => player.ControllerId)
            .Select(player => $"{player.ControllerId},{player.CharacterObjectId},{player.MobilePartyId}");

        return string.Join("|", players);
    }

    [CommandLineArgumentFunction("enable", "coop.debug.location_conversation")]
    public static string Enable(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.enable";
        if (ModInformation.IsServer) return "Command is only available to run on a client";

        LocationConversationLiveTestProbe.Enable();
        return "Location conversation live-test mode enabled";
    }

    [CommandLineArgumentFunction("disable", "coop.debug.location_conversation")]
    public static string Disable(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.disable";
        if (ModInformation.IsServer) return "Command is only available to run on a client";

        if (LocationConversationLiveTestProbe.HasApprovedEngagement ||
            LocationPlayerInteractionWaitingOverlay.Instance.IsShownForLiveTest)
        {
            return "End the active synthetic interaction before disabling live-test mode";
        }

        LocationConversationLiveTestProbe.Disable();
        return "Location conversation live-test mode disabled";
    }

    [CommandLineArgumentFunction("request", "coop.debug.location_conversation")]
    public static string Request(List<string> args)
    {
        if (args.Count != 2)
            return "Usage: coop.debug.location_conversation.request <targetControllerId> <generation>";
        if (ModInformation.IsServer) return "Command is only available to run on a client";
        if (!LocationConversationLiveTestProbe.Enabled) return "Enable location conversation live-test mode first";
        if (!int.TryParse(args[1], out var generation)) return $"Invalid generation '{args[1]}'";
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return $"Unable to resolve {nameof(IPlayerManager)}";
        if (!ContainerProvider.TryResolve<INetwork>(out var network))
            return $"Unable to resolve {nameof(INetwork)}";

        var target = playerManager.Players.SingleOrDefault(player => player.ControllerId == args[0]);
        if (target == null) return $"Unable to find player '{args[0]}'";
        if (string.IsNullOrEmpty(target.CharacterObjectId))
            return $"Player '{args[0]}' has no character object id";

        network.SendAll(new NetworkRequestLocationConversation(
            SyntheticLocationId,
            target.CharacterObjectId,
            generation));

        return $"Requested location conversation with '{args[0]}' using generation {generation}";
    }

    [CommandLineArgumentFunction("end", "coop.debug.location_conversation")]
    public static string End(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.end";
        if (ModInformation.IsServer) return "Command is only available to run on a client";
        if (!ContainerProvider.TryResolve<INetwork>(out var network))
            return $"Unable to resolve {nameof(INetwork)}";

        network.SendAll(new NetworkLocationConversationEnded());
        LocationConversationLiveTestProbe.RecordEnded();
        return "Ended location conversation";
    }

    [CommandLineArgumentFunction("status", "coop.debug.location_conversation")]
    public static string Status(List<string> args)
    {
        if (args.Count != 0) return "Usage: coop.debug.location_conversation.status";
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager))
            return $"Unable to resolve {nameof(IPlayerManager)}";
        if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider))
            return $"Unable to resolve {nameof(IControllerIdProvider)}";

        var trackerState = ModInformation.IsServer
            ? (LocationConversationTracker.Instance?.IsEmpty.ToString() ?? "<unavailable>")
            : "<n/a>";
        var overlay = LocationPlayerInteractionWaitingOverlay.Instance;

        return $"enabled={LocationConversationLiveTestProbe.Enabled};" +
               $"controller={controllerIdProvider.ControllerId};" +
               $"players={playerManager.Players.Count};" +
               $"lastAllowed={LocationConversationLiveTestProbe.LastAllowedGeneration};" +
               $"lastDenied={LocationConversationLiveTestProbe.LastDeniedGeneration};" +
               $"hasApproved={LocationConversationLiveTestProbe.HasApprovedEngagement};" +
               $"overlayShown={overlay.IsShownForLiveTest};" +
               $"overlayText={overlay.WaitingTextForLiveTest ?? "<none>"};" +
               $"trackerEmpty={trackerState}";
    }

    [CommandLineArgumentFunction("mark", "coop.debug.location_conversation")]
    public static string Mark(List<string> args)
    {
        if (args.Count != 1) return "Usage: coop.debug.location_conversation.mark <label>";
        if (ModInformation.IsServer) return "Command is only available to run on a client";

        var label = args[0];
        GameThread.Run(
            () => InformationManager.DisplayMessage(new InformationMessage($"LIVE TEST: {label}")),
            blocking: true);
        return $"Displayed live-test label '{label}'";
    }
}
#endif
