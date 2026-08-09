using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network.Session;
using Coop.GOG;
using Coop.Steam;
using Serilog;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.PlatformService;

namespace Coop
{
    /// <summary>Selects the session adapter for the storefront that launched this process.</summary>
    internal static class PlatformSessionIntegrationBoot
    {
        private static readonly ILogger Logger = LogManager.GetLogger(typeof(PlatformSessionIntegrationBoot));

        private static ISessionProvider provider;
        private static bool started;

        public static IUpdateable TryStart(
            bool isServerProcess,
            string commandLine,
            ISessionJoinRequestGate joinRequestGate)
        {
            if (started) return null;
            started = true;

            string providerName = PlatformServices.ProviderName;
            try
            {
                provider = isServerProcess
                    ? TryCreateServerProvider(providerName)
                    : TryCreateClientProvider(providerName, commandLine, joinRequestGate);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "{Provider} session integration unavailable", providerName);
            }

            if (isServerProcess)
                SessionDiscovery.ServerProvider = provider;
            else
                SessionDiscovery.ClientProvider = provider;

            if (provider == null)
            {
                Logger.Information(
                    "Platform session integration inactive for provider {Provider}",
                    providerName);
                return null;
            }

            Logger.Information("{Provider} session integration active", provider.DisplayName);
            return provider.CallbackPump;
        }

        public static void Dispose()
        {
            ISessionProvider activeProvider = provider;
            provider = null;

            if (ReferenceEquals(SessionDiscovery.ClientProvider, activeProvider))
                SessionDiscovery.ClientProvider = null;
            if (ReferenceEquals(SessionDiscovery.ServerProvider, activeProvider))
                SessionDiscovery.ServerProvider = null;

            activeProvider?.Dispose();
        }

        private static ISessionProvider TryCreateClientProvider(
            string providerName,
            string commandLine,
            ISessionJoinRequestGate joinRequestGate)
        {
            if (string.Equals(providerName, "Steam", StringComparison.OrdinalIgnoreCase))
                return TryCreateSteamClient(commandLine, joinRequestGate);
            if (string.Equals(providerName, "GOG", StringComparison.OrdinalIgnoreCase))
                return TryCreateGalaxyClient(joinRequestGate);

            return null;
        }

        private static ISessionProvider TryCreateServerProvider(string providerName)
        {
            if (string.Equals(providerName, "Steam", StringComparison.OrdinalIgnoreCase))
                return TryCreateSteamServer();
            if (string.Equals(providerName, "GOG", StringComparison.OrdinalIgnoreCase))
                return TryCreateGalaxyServer();

            return null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ISessionProvider TryCreateSteamClient(
            string commandLine,
            ISessionJoinRequestGate joinRequestGate) =>
            SteamSessionProvider.TryCreateClient(MessageBroker.Instance, commandLine, joinRequestGate);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ISessionProvider TryCreateSteamServer() =>
            SteamSessionProvider.TryCreateServer();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ISessionProvider TryCreateGalaxyClient(ISessionJoinRequestGate joinRequestGate) =>
            GalaxySessionProvider.TryCreateClient(MessageBroker.Instance, joinRequestGate);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ISessionProvider TryCreateGalaxyServer() =>
            GalaxySessionProvider.TryCreateServer();
    }
}
