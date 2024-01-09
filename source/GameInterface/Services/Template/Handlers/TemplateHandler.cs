using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Handlers;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Patches;
using GameInterface.Services.Template.Messages;
using Serilog;
using System;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Template.Handlers;

/// <summary>
/// TODO update summary
/// Handlers are auto-instantiated by <see cref="ServiceModule"/>
/// </summary>
public class TemplateHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TemplateHandler>();

    private readonly IMessageBroker messageBroker;

    // TODO remove explanitory comments
    // Our dependency injection framework (autofac) will automatically resolve interfaces and pass them to the constructor
    // For this example, a messageBroker instance is automatically passed to this constructor.
    // You can pass as many interfaces as you want to the constructor as long as the interface is registered int GameInterfaceModule
    public TemplateHandler(IMessageBroker messageBroker)
    {
        // TODO remove explanitory comments
        // When TemplateCommandMessage is published to the message broker, Handle_TemplateCommandMessage is called
        messageBroker.Subscribe<TemplateCommandMessage>(Handle_TemplateCommandMessage);
    }

    public void Dispose()
    {
        // TODO remove explanitory comments
        // Clean up subscriptions so the message broker does not keep this instance alive.
        // Delegates attach the instance so if that delegate is stored somewhere the garbage collecter will not collect this instance
        // The current implementation 
        messageBroker.Unsubscribe<TemplateCommandMessage>(Handle_TemplateCommandMessage);
    }

    private void Handle_TemplateCommandMessage(MessagePayload<TemplateCommandMessage> payload)
    {
        // TODO remove explanitory comments
        // Publishing a message to all internal software is done using the message broker
        messageBroker.Publish(this, new TemplateEventMessage());
    }
}
