using Coop.Core.Client.States;
using GameInterface.Policies;
using System;
using System.Collections.Generic;

namespace Coop.Core.Client.Policies;

/// <summary>
/// Client side sync policy
/// </summary>
/// <inheritdoc cref="ISyncPolicy"/>
internal class ClientSyncPolicy : ISyncPolicy
{
    private readonly IClientLogic clientLogic;

    public ClientSyncPolicy(IClientLogic clientLogic)
    {
        this.clientLogic = clientLogic;
    }

    private readonly HashSet<Type> syncStates = new HashSet<Type>
    {
        typeof(CampaignState),
        typeof(MissionState)
    };

    public bool AllowOriginal()
    {
        // Allow original calls if not in map state or mission state
        return syncStates.Contains(clientLogic.State.GetType()) == false;
    }
}
