using Common.Messaging;
using Steamworks;
using System;
using System.Runtime.CompilerServices;

namespace Coop.Steam;

/// <summary>
/// Probes the game-initialized Steam runtime and starts the process-lifetime Steam services.
/// The probe and service creation are separate non-inlined methods so a non-Steam install
/// (no Steamworks.NET.dll in the game bin) fails with a catchable load exception here
/// instead of poisoning the caller.
/// </summary>
public static class SteamBoot
{
    // Strong root: MessageBroker subscriptions are weak references, so the listener must be
    // reachable for the process lifetime or its subscriptions silently die.
    public static SteamJoinListener JoinListener { get; private set; }

    // Created before any session container exists, so it lives here rather than in DI.
    public static SteamTunnelJoinEndpointPreparer TunnelPreparer { get; private set; }

    public static bool TryStart(IMessageBroker messageBroker, string commandLine)
    {
        if (JoinListener != null) return true;

        bool available;
        try
        {
            available = ProbeSteam();
        }
        catch (Exception)
        {
            // Steamworks.NET.dll absent (non-Steam install) or SteamAPI not initialized.
            available = false;
        }

        if (!available) return false;

        CreateServices(messageBroker, commandLine);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ProbeSteam()
    {
        return SteamAPI.IsSteamRunning() && SteamUser.GetSteamID().IsValid();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateServices(IMessageBroker messageBroker, string commandLine)
    {
        JoinListener = new SteamJoinListener(messageBroker, new SteamLobbyApi());
        TunnelPreparer = new SteamTunnelJoinEndpointPreparer();
        JoinListener.ProcessLaunchArguments(commandLine);
    }
}
