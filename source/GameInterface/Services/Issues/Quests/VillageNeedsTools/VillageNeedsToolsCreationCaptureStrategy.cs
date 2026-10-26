using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue;

namespace GameInterface.Services.Issues.Quests.VillageNeedsTools;

/// <summary>Preserves the server's offer, including its choice of goods or gold payment.</summary>
internal class VillageNeedsToolsCreationCaptureStrategy : ICreationCaptureStrategy
{
    // Publicizing exposes these fields but leaves the constructor-only assignments readonly.
    private static readonly FieldInfo ExchangeItem = AccessTools.Field(typeof(Issue), nameof(Issue._exchangeItem));
    private static readonly FieldInfo ExchangeCount = AccessTools.Field(typeof(Issue), nameof(Issue._numberOfExchangeItem));
    private static readonly FieldInfo RequestedCount = AccessTools.Field(typeof(Issue), nameof(Issue._numberOfRequestedItem));
    private static readonly FieldInfo Payment = AccessTools.Field(typeof(Issue), nameof(Issue._payment));

    private readonly IObjectManager objectManager;

    public VillageNeedsToolsCreationCaptureStrategy(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        var toolsIssue = (Issue)issue;
        if (!objectManager.TryGetIdWithLogging(toolsIssue._requestedItem, out var requestedId))
        {
            throw new InvalidOperationException("The tools offer has an unregistered requested item");
        }

        var exchangeId = string.Empty;
        if (toolsIssue._exchangeItem != null && !objectManager.TryGetIdWithLogging(toolsIssue._exchangeItem, out exchangeId))
        {
            throw new InvalidOperationException("The tools offer has an unregistered exchange item");
        }

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(requestedId);
            writer.Write(exchangeId);
            writer.Write(toolsIssue._numberOfRequestedItem);
            writer.Write(toolsIssue._numberOfExchangeItem);
            writer.Write(toolsIssue._payment);
            return stream.ToArray();
        }
    }

    public IssueBase CreateIssue(Hero issueOwner, byte[] captured)
    {
        using (var stream = new MemoryStream(captured))
        using (var reader = new BinaryReader(stream))
        {
            if (!objectManager.TryGetObjectWithLogging<ItemObject>(reader.ReadString(), out var requestedItem))
            {
                throw new InvalidOperationException("The tools offer's requested item is missing");
            }

            var exchangeId = reader.ReadString();
            ItemObject exchangeItem = null;
            if (exchangeId.Length != 0 && !objectManager.TryGetObjectWithLogging(exchangeId, out exchangeItem))
            {
                throw new InvalidOperationException("The tools offer's exchange item is missing");
            }

            var issue = new Issue(issueOwner, requestedItem);
            ExchangeItem.SetValue(issue, exchangeItem);
            RequestedCount.SetValue(issue, reader.ReadInt32());
            ExchangeCount.SetValue(issue, reader.ReadInt32());
            Payment.SetValue(issue, reader.ReadInt32());
            return issue;
        }
    }

    public void ApplyAfterCreation(IssueBase issue, byte[] captured)
    {
    }

    public bool TryBuildDebugCapture(Hero issueOwner, out byte[] captured)
    {
        captured = null;
        if (!issueOwner.IsHeadman || issueOwner.CurrentSettlement?.IsVillage != true)
        {
            return false;
        }

        captured = Capture(new Issue(issueOwner, DefaultItems.Tools));
        return true;
    }
}
