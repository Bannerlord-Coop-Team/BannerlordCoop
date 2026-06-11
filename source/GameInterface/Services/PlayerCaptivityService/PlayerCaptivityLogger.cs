using Common.Logging;
using Serilog;
using Serilog.Core;
using System;

namespace GameInterface.Services.PlayerCaptivityService;

/// <summary>
/// Diagnostic logger for the player captivity flow. Every call is a no-op unless
/// <see cref="PlayerCaptivityConfig.Debug"/> is enabled, so verbose captivity tracing can be left in
/// the code without polluting normal play sessions. All output is tagged with the
/// "PlayerConfigService" group for easy filtering in Seq.
/// </summary>
internal class PlayerCaptivityLogger
{
    private static readonly ILogger Logger = LogManager
        .GetLogger<PlayerCaptivityLogger>()
        .ForContext("Group", "PlayerConfigService");

    public static void Verbose(string messageTemplate, params object[] propertyValues)
    {
        if (!PlayerCaptivityConfig.Debug) return;
        Logger.Verbose(messageTemplate, propertyValues);
    }

    public static void Debug(string messageTemplate, params object[] propertyValues)
    {
        if (!PlayerCaptivityConfig.Debug) return;
        Logger.Debug(messageTemplate, propertyValues);
    }

    public static void Information(string messageTemplate, params object[] propertyValues)
    {
        if (!PlayerCaptivityConfig.Debug) return;
        Logger.Information(messageTemplate, propertyValues);
    }

    public static void Warning(string messageTemplate, params object[] propertyValues)
    {
        if (!PlayerCaptivityConfig.Debug) return;
        Logger.Warning(messageTemplate, propertyValues);
    }

    public static void Error(string messageTemplate, params object[] propertyValues)
    {
        if (!PlayerCaptivityConfig.Debug) return;
        Logger.Error(messageTemplate, propertyValues);
    }

    public static void Error(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        if (!PlayerCaptivityConfig.Debug) return;
        Logger.Error(exception, messageTemplate, propertyValues);
    }
}
