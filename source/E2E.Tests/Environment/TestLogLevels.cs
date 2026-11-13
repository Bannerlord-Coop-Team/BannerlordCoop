using Common.Logging;
using GameInterface.Services.MapEvents.Handlers;
using Serilog.Events;
using System.Runtime.CompilerServices;

namespace E2E.Tests.Environment;

/// <summary>Raises the test log level for the few sources whose Verbose lines a test checks.</summary>
internal static class TestLogLevels
{
    // Loggers read the configuration at their first log event, so this runs when the test assembly loads.
    [ModuleInitializer]
    internal static void Apply()
    {
        // The server-detected conversation deferral is Verbose, and PlayerPartyInteractionFlowTests counts it.
        LogManager.Configuration.MinimumLevel.Override(typeof(ConversationRequestHandler).FullName, LogEventLevel.Verbose);
    }
}
