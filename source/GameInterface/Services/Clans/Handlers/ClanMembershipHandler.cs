using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanMembershipHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IClanLeaveRules leaveRules;

    public ClanMembershipHandler(IMessageBroker messageBroker, IObjectManager objectManager,
        INetwork network, IClanLeaveRules leaveRules)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.leaveRules = leaveRules;
        messageBroker.Subscribe<ClanMemberLeaveRequested>(Handle);
        messageBroker.Subscribe<RequestClanMemberLeave>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ClanMemberLeaveRequested>(Handle);
        messageBroker.Unsubscribe<RequestClanMemberLeave>(Handle);
    }

    private void Handle(MessagePayload<ClanMemberLeaveRequested> payload)
    {
        if (ModInformation.IsServer) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Actor, out var actorId) ||
            !objectManager.TryGetIdWithLogging(payload.What.Member, out var memberId)) return;

        network.SendAll(new RequestClanMemberLeave(actorId, memberId));
    }

    private void Handle(MessagePayload<RequestClanMemberLeave> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(payload.What.ActorId, out var actor) ||
                !objectManager.TryGetObjectWithLogging<Hero>(payload.What.MemberId, out var member)) return;
            if (actor == member ? leaveRules.CanLeave(member) : leaveRules.CanRemove(actor, member))
                leaveRules.TryApply(member);
        });
    }
}
