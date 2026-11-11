using Autofac;
using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Smithing.Messages;
using GameInterface.Services.Smithing.Patches;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.Smithing;

/// <summary>Verifies the town order prefix announces the town vanilla passes to it.</summary>
[Collection(ModInformationRoleCollection.Name)]
public sealed class TownOrdersPatchesTests
{
    private static List<TownOrderCreated> RunCreateTownOrderPrefix(Town town, Hero orderOwner, int orderSlot, bool isServer, out bool runOriginal)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(Mock.Of<ISyncPolicy>(policy => policy.AllowOriginal() == false)).As<ISyncPolicy>();
        using var container = builder.Build();
        ContainerProvider.TryGetContainer(out var previousContainer);
        var published = new List<TownOrderCreated>();
        Action<MessagePayload<TownOrderCreated>> capture = payload => published.Add(payload.What);
        bool originalIsServer = ModInformation.IsServer;
        MessageBroker.Instance.Subscribe(capture);
        try
        {
            ModInformation.IsServer = isServer;
            ContainerProvider.SetContainer(container);
            var behavior = (CraftingCampaignBehavior)FormatterServices.GetUninitializedObject(typeof(CraftingCampaignBehavior));
            runOriginal = TownOrdersPatches.CreateTownOrderPrefix(ref behavior, town, orderOwner, orderSlot);
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
            if (previousContainer != null) ContainerProvider.SetContainer(previousContainer);
            else ContainerProvider.Clear();
            MessageBroker.Instance.Unsubscribe(capture);
        }
        return published;
    }

    [Fact]
    public void CreateTownOrderPrefix_OnServer_AnnouncesTheTownItWasGiven()
    {
        // v1.5.7 passes the town, because the order owner need not stand in it. This owner has no settlement.
        var town = (Town)FormatterServices.GetUninitializedObject(typeof(Town));
        var orderOwner = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

        var published = RunCreateTownOrderPrefix(town, orderOwner, orderSlot: 3, isServer: true, out bool runOriginal);

        Assert.False(runOriginal);
        TownOrderCreated order = Assert.Single(published);
        Assert.Same(town, order.Town);
        Assert.Same(orderOwner, order.OrderOwner);
        Assert.Equal(3, order.OrderSlot);
    }

    [Fact]
    public void CreateTownOrderPrefix_OnClient_SkipsVanillaAndAnnouncesNothing()
    {
        var town = (Town)FormatterServices.GetUninitializedObject(typeof(Town));
        var orderOwner = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

        var published = RunCreateTownOrderPrefix(town, orderOwner, orderSlot: 0, isServer: false, out bool runOriginal);

        Assert.False(runOriginal);
        Assert.Empty(published);
    }
}
