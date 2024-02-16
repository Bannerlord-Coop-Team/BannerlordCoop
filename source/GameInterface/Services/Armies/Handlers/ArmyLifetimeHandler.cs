using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Handlers;

/// <summary>
/// Handler for <see cref="Army"/> messages
/// </summary>
public class ArmyLifetimeHandler : IHandler
{
    
    private static readonly ILogger Logger = LogManager.GetLogger<ArmyHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public ArmyLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<CreateArmy>(Handle_CreateArmy);
        messageBroker.Subscribe<DestroyArmy>(Handle_DestroyArmy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CreateArmy>(Handle_CreateArmy);
        messageBroker.Unsubscribe<DestroyArmy>(Handle_DestroyArmy);
    }

    private void Handle_CreateArmy(MessagePayload<CreateArmy> payload)
    {
        var stringId = payload.What.Data.StringId;

        ArmyLifetimePatches.OverrideCreateArmy(stringId);
    }

    private void Handle_DestroyArmy(MessagePayload<DestroyArmy> payload)
    {
        var stringId = payload.What.Data.StringId;
        var reason = (Army.ArmyDispersionReason)payload.What.Data.Reason;

        if(objectManager.TryGetObject(stringId, out Army army) == false)
        {
            Logger.Error("Failed to find army with stringId {stringId}", stringId);
            return;
        }

        if (objectManager.Remove(army) == false)
        {
            Logger.Error("Failed to remove army with stringId {stringId}", stringId);
            return;
        }

        ArmyLifetimePatches.OverrideDestroyArmy(army, reason);
    }
}
