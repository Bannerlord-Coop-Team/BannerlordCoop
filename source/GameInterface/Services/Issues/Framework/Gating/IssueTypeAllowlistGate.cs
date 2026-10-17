using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Gating;

/// <summary>
/// Keeps every vanilla issue behavior that has no registered quest type from registering its
/// events, so an issue that is not synced can never be generated.
/// </summary>
[HarmonyPatch]
internal class IssueTypeAllowlistGate
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueTypeAllowlistGate>();

    private static readonly string[] IssueAssemblyNames = { "TaleWorlds.CampaignSystem", "SandBox" };

    private static IEnumerable<Type> FindIssueTypes()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!IssueAssemblyNames.Contains(assembly.GetName().Name))
            {
                continue;
            }

            foreach (var type in GetLoadableTypes(assembly))
            {
                var isConcreteIssue = !type.IsAbstract && typeof(IssueBase).IsAssignableFrom(type);

                if (isConcreteIssue && typeof(CampaignBehaviorBase).IsAssignableFrom(type.DeclaringType))
                {
                    yield return type;
                }
            }
        }
    }

    private static IEnumerable<Type> FindIssueBehaviorTypes()
    {
        return FindIssueTypes().Select(issueType => issueType.DeclaringType).Distinct();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(type => type != null);
        }
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var behaviorType in FindIssueBehaviorTypes())
        {
            var registerEvents = AccessTools.DeclaredMethod(behaviorType, nameof(CampaignBehaviorBase.RegisterEvents));

            if (registerEvents != null)
            {
                yield return registerEvents;
            }
        }
    }

    [HarmonyPrefix]
    private static bool Prefix(CampaignBehaviorBase __instance)
    {
        if (!ContainerProvider.TryResolve<IQuestTypeRegistry>(out var registry))
        {
            return false;
        }

        var behaviorType = __instance.GetType();

        var isRegistered = registry.Descriptors.Any(descriptor => descriptor.IssueType.DeclaringType == behaviorType);

        if (!isRegistered)
        {
            Logger.Verbose("Issue behavior {behavior} is not registered, its events stay off", behaviorType.Name);
        }

        return isRegistered;
    }
}
