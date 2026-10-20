using GameInterface.AutoSync;
using GameInterface.AutoSync.Builders;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using System;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;

namespace GameInterface.Tests.AutoSync;

public class AutoSyncPatchBuilderTests
{
    public class NestedTarget
    {
        public void Update()
        {
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("quest-callback")]
    public void NestedTarget_KeepsItsNamespaceAndRendersValidCSharp(string category)
    {
        // No members are registered, so none of the member-builder collaborators are used.
        var builder = (AutoSyncPatchBuilder)FormatterServices.GetUninitializedObject(typeof(AutoSyncPatchBuilder));
        var registration = new AutoSyncRegistryItem();
        registration.AddTargetMethod(AccessTools.Method(typeof(NestedTarget), nameof(NestedTarget.Update)), category);

        var trees = builder.Build(typeof(Town), registration);
        var expected = "typeof(" + typeof(NestedTarget).FullName.Replace('+', '.') + ")";

        Assert.Contains(trees, tree => tree.ToString().Contains(expected));
        Assert.Empty(trees.SelectMany(tree => tree.GetDiagnostics()).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }
}
