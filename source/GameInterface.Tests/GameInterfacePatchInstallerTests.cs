using GameInterface.Tests.Services.Issues;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests;

[Collection(nameof(CampaignCurrentCollection))]
public class GameInterfacePatchInstallerTests
{
    private static List<Type> QuestPatchesOwnedBy(Harmony harmony)
    {
        return harmony.GetPatchedMethods()
            .Select(Harmony.GetPatchInfo)
            .SelectMany(info => info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers))
            .Where(patch => patch.owner == harmony.Id)
            .Select(patch => patch.PatchMethod.DeclaringType)
            .Where(type => type.GetCustomAttribute<HarmonyPatchCategory>()?.info.category == GameInterface.HARMONY_QUESTS_CATEGORY)
            .Distinct()
            .ToList();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Install_AppliesQuestPatchesOnlyWhenQuestsAreOn(bool questsOn)
    {
        var harmony = new Harmony($"{nameof(GameInterfacePatchInstallerTests)}.{Guid.NewGuid():N}");

        try
        {
            new GameInterfacePatchInstaller(new TestQuestFeatureFlag(questsOn)).Install(harmony);

            Assert.NotEmpty(harmony.GetPatchedMethods());
            Assert.Equal(questsOn, QuestPatchesOwnedBy(harmony).Count > 0);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
