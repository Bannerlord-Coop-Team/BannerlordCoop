using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using LiteNetLib;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.MerchantNeedsHelpWithOutlaws;

public readonly struct MerchantOutlawsAlternativeCancelled : IEvent
{
    public IssueBase Issue { get; }

    public MerchantOutlawsAlternativeCancelled(IssueBase issue) => Issue = issue;
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestMerchantOutlawsAlternativeCancellation : ICommand
{
    [ProtoMember(3)] public TroopRosterElementData[] Troops { get; }

    public RequestMerchantOutlawsAlternativeCancellation(TroopRosterElementData[] troops)
    {
        Troops = troops;
    }
}

internal class MerchantOutlawsAlternativeCancellationHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IAlternativeSolutionTroopValidator troops;

    public MerchantOutlawsAlternativeCancellationHandler(IMessageBroker messageBroker, INetwork network,
        IObjectManager objectManager, IPlayerManager playerManager,
        IAlternativeSolutionTroopValidator troops)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.troops = troops;
        messageBroker.Subscribe<MerchantOutlawsAlternativeCancelled>(Handle_Local);
        messageBroker.Subscribe<RequestMerchantOutlawsAlternativeCancellation>(Handle_Request);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MerchantOutlawsAlternativeCancelled>(Handle_Local);
        messageBroker.Unsubscribe<RequestMerchantOutlawsAlternativeCancellation>(Handle_Request);
    }

    private void Handle_Local(MessagePayload<MerchantOutlawsAlternativeCancelled> payload)
    {
        if (ModInformation.IsServer) return;
        var issue = payload.What.Issue;
        network.SendAll(new RequestMerchantOutlawsAlternativeCancellation(
            troops.ToData(issue.AlternativeSolutionSentTroops)));
        using (new AllowedThread()) troops.ReturnToMainParty(issue);
    }

    private void Handle_Request(MessagePayload<RequestMerchantOutlawsAlternativeCancellation> payload)
    {
        if (ModInformation.IsClient) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!(payload.Who is NetPeer peer) || !playerManager.TryGetPlayer(peer, out var player) ||
                !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
                hero.PartyBelongedTo == null) return;

            // The roster screen already committed troop removal; declining the follow-up must undo it on the server.
            troops.ReturnTroopsToParty(hero.PartyBelongedTo, data.Troops);
        }, context: nameof(MerchantOutlawsAlternativeCancellationHandler));
    }
}
