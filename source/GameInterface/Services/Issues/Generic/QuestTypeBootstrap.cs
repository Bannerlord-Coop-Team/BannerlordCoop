using Common.Extensions;
using Common.Logging;
using Serilog;
using System;
using System.Runtime.CompilerServices;

namespace GameInterface.Services.Issues.Generic;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class QuestTypeModuleAttribute : Attribute
{
}

internal static class QuestTypeBootstrap
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(QuestTypeBootstrap));

    private const string MigratedNamespace = "GameInterface.Services.Issues.Generic.Migrated";

    internal static void EnsureAllMigratedTypesRegistered()
    {
        foreach (var type in AppDomain.CurrentDomain.GetDomainTypes(MigratedNamespace))
        {
            if (!type.IsDefined(typeof(QuestTypeModuleAttribute), inherit: false)) continue;

            try
            {
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to register quest type module {Type} - every other migrated quest type still registers normally", type);
            }
        }
    }
}
