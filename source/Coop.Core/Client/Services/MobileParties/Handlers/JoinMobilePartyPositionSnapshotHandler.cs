using Common;
using Common.Messaging;
using Common.Util;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Client.Services.MobileParties.Handlers;

/// <summary>
/// Applies the authoritative map positions captured after a joining client's held stream was flushed.
/// </summary>
public sealed class JoinMobilePartyPositionSnapshotHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public JoinMobilePartyPositionSnapshotHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkJoinMobilePartyPositions>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkJoinMobilePartyPositions>(Handle);
    }

    private void Handle(MessagePayload<NetworkJoinMobilePartyPositions> payload)
    {
        var positions = payload.What.Positions;
        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                foreach (var position in positions)
                {
                    if (!objectManager.TryGetObjectWithLogging(position.MobilePartyId, out MobileParty party))
                        continue;

                    party.Position = position.ToCampaignVec2();
                }
            }
        }, context: nameof(JoinMobilePartyPositionSnapshotHandler));
    }
}
