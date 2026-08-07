using Common.Extensions;
using System;
using System.Runtime.CompilerServices;

namespace GameInterface.Services.Issues.Generic;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class QuestTypeModuleAttribute : Attribute
{
}

internal static class QuestTypeBootstrap
{
    private const string MigratedNamespace = "GameInterface.Services.Issues.Generic.Migrated";

    internal static void EnsureAllMigratedTypesRegistered()
    {
        foreach (var type in AppDomain.CurrentDomain.GetDomainTypes(MigratedNamespace))
        {
            if (type.IsDefined(typeof(QuestTypeModuleAttribute), inherit: false))
            {
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
        }
    }
}
