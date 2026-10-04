using GameInterface.Services.Issues.Patches;
using HarmonyLib;
using Common;
using Common.Util;
using GameInterface.Policies;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Actions;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

[Collection(nameof(ModInformationRoleCollection))]
public class ExtortionQuestPatchTests
{
    [Fact]
    public void LoadingOriginalsDoNotRunPersonalQuestActionsBeforeThePlayerIsBound()
    {
        var harmony = new Harmony(nameof(LoadingOriginalsDoNotRunPersonalQuestActionsBeforeThePlayerIsBound));
        var wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = false;
            harmony.CreateClassProcessor(typeof(ExtortionQuestLoadPatch)).Patch();
            harmony.CreateClassProcessor(typeof(ExtortionQuestWorldCallbackPatches)).Patch();
            harmony.CreateClassProcessor(typeof(ExtortionQuestCompletionPatches)).Patch();
            var quest = ObjectHelper.SkipConstructor<ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest>();
            using (CallOriginalPolicy.AllowOriginalsOnAllThreads())
            using (new AllowedThread())
            {
                quest.InitializeQuestOnGameLoad();
                quest.OnWarDeclared(null, null, DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility);
                quest.CompleteQuestWithCancel();
                Assert.False(quest.IsFinalized);
            }
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void AllExtortionPatchesBindToInstalledGameSignatures()
    {
        var harmony = new Harmony(nameof(AllExtortionPatchesBindToInstalledGameSignatures));
        try
        {
            var patchTypes = typeof(ExtortionQuestCreationPatches).Assembly.GetTypes()
                .Where(type => type.Namespace == typeof(ExtortionQuestCreationPatches).Namespace &&
                    type.Name.StartsWith("Extortion", StringComparison.Ordinal));
            foreach (var type in patchTypes) Assert.NotEmpty(harmony.CreateClassProcessor(type).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
