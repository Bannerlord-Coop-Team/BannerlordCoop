using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using Behavior = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue;

namespace GameInterface.Services.Issues.Quests.VillageNeedsCraftingMaterials;

internal class VillageNeedsCraftingMaterialsCreationCaptureStrategy : ICreationCaptureStrategy
{
    private static readonly FieldInfo RequestedItemField = AccessTools.Field(typeof(Issue), nameof(Issue._requestedItem));

    private readonly IObjectManager objectManager;

    public VillageNeedsCraftingMaterialsCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        objectManager.TryGetIdWithLogging(((Issue)issue)._requestedItem, out var requestedItemId);

        return Write(requestedItemId ?? string.Empty);
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        var issue = new Issue(issueOwner);

        // The constructor rolls the material locally, the server's roll replaces it
        if (objectManager.TryGetObjectWithLogging<ItemObject>(Read(captured), out var requestedItem))
        {
            RequestedItemField.SetValue(issue, requestedItem);
        }

        return issue;
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;

        if (!issueOwner.IsNotable || issueOwner.CurrentSettlement?.IsVillage != true)
        {
            return false;
        }

        if (!objectManager.TryGetIdWithLogging(Behavior.SelectCraftingMaterial(), out var requestedItemId))
        {
            return false;
        }

        captured = Write(requestedItemId);
        return true;
    }

    private static byte[] Write(string requestedItemId)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(requestedItemId);
                writer.Flush();

                return stream.ToArray();
            }
        }
    }

    private static string Read(byte[] captured)
    {
        using (var stream = new MemoryStream(captured))
        {
            using (var reader = new BinaryReader(stream))
            {
                return reader.ReadString();
            }
        }
    }
}
