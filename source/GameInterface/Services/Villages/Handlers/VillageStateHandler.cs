﻿using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Villages.Messages;
using Serilog;
using System;

namespace GameInterface.Services.Villages.Handlers;

/// <summary>
/// TODO update summary
/// Handlers are auto-instantiated by <see cref="ServiceModule"/>
/// </summary>
public class VillageStateHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageStateHandler>();

    private readonly IMessageBroker messageBroker;

    // TODO remove explanitory comments
    // Our dependency injection framework (autofac) will automatically resolve interfaces and pass them to the constructor
    // For this example, a messageBroker instance is automatically passed to this constructor.
    // You can pass as many interfaces as you want to the constructor as long as the interface is registered int GameInterfaceModule
    public VillageStateHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker; 
        messageBroker.Subscribe<VillageStateChange>(Handle);
        messageBroker.Subscribe<VillageChangeState>(Handle);
    }

    // this should be modified in the registry.
    private void Handle(MessagePayload<VillageChangeState> payload)
    {
        throw new NotImplementedException();
    }

    // All the village should not be Registried through here..
    private void Handle(MessagePayload<VillageStateChange> obj)
    {
        var payload = obj.What.VillageChanged;
        //throw new NotImplementedException();
    }

    public void Dispose()
    {
        // TODO remove explanitory comments
        // Clean up subscriptions so the message broker does not keep this instance alive.
        // Delegates attach the instance so if that delegate is stored somewhere the garbage collecter will not collect this instance
        // The current implementation 
        messageBroker?.Unsubscribe<VillageStateChange>(Handle);
    }
}