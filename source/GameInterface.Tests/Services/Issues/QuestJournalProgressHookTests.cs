using GameInterface.Services.Issues.Framework.Dispatch;
using GameInterface.Utils;
using HarmonyLib;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

/// <summary>Checks the progress hook against the Windows guard before Harmony publishes its patch metadata.</summary>
public class QuestJournalProgressHookTests
{
    [Fact]
    public void FirstProgressHookIsNotDiscardedBeforeItsPatchMetadataIsPublished()
    {
        var declaration = Assert.Single(typeof(QuestJournalProgressPatch).GetCustomAttributes<HarmonyPatch>());
        var target = declaration.info.methodType == MethodType.Setter
            ? AccessTools.PropertySetter(declaration.info.declaringType, declaration.info.methodName)
            : AccessTools.Method(declaration.info.declaringType, declaration.info.methodName);

        Assert.NotNull(target);
        // During DetourMethod, Harmony.GetPatchInfo still exposes the previous patch set.
        Assert.False(FragileDetourGuard.ShouldSkipDetour(target, _ => true, _ => false));
    }
}
