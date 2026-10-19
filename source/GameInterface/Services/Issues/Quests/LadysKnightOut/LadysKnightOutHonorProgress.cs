using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using ProtoBuf;
using System.Linq;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal interface ILadysKnightOutHonorProgress
{
    void Seed(IssueBase issue, int xp);
    void MirrorClientHonor(int xp);
    void MirrorServerHonor(string controllerId, int xp);
    void ApplyPenalty(QuestBase quest);
}

internal sealed class LadysKnightOutHonorProgress : ILadysKnightOutHonorProgress
{
    private readonly Dictionary<string, int> honorXp = new Dictionary<string, int>();
    private readonly IIssueOwnerResolver owners;
    private readonly IObjectManager objects;
    private readonly INetwork network;
    private readonly IControllerIdProvider controller;

    public LadysKnightOutHonorProgress(
        IIssueOwnerResolver owners,
        IObjectManager objects,
        INetwork network,
        IControllerIdProvider controller)
    {
        this.owners = owners;
        this.objects = objects;
        this.network = network;
        this.controller = controller;
    }

    public void Seed(IssueBase issue, int xp)
    {
        if (owners.TryGetOwnerControllerId(issue, out var controllerId)) honorXp[controllerId] = xp;
    }

    public void MirrorClientHonor(int xp)
    {
        if (ModInformation.IsServer || !Campaign.Current.IssueManager.Issues.Values
                .OfType<IssueBase>()
                .Any(issue => issue.IssueOwner == Hero.MainHero && issue.IssueQuest != null)) return;
        network.SendAll(new RequestLadysKnightOutHonorMirror(controller.ControllerId, xp));
    }

    public void MirrorServerHonor(string controllerId, int xp)
    {
        if (ModInformation.IsServer && honorXp.ContainsKey(controllerId)) honorXp[controllerId] = xp;
    }

    public void ApplyPenalty(QuestBase quest)
    {
        var issue = quest.QuestGiver.Issue;
        if (ModInformation.IsClient || issue == null || !owners.TryResolveOwnerHero(issue, out var hero) ||
            !owners.TryGetOwnerControllerId(issue, out var controllerId) || !objects.TryGetIdWithLogging(hero, out var heroId)) return;
        var model = Campaign.Current.Models.CharacterDevelopmentModel;
        if (!honorXp.TryGetValue(controllerId, out var xp)) xp = model.GetTraitXpRequiredForTraitLevel(DefaultTraits.Honor, hero.GetTraitLevel(DefaultTraits.Honor));
        model.GetTraitLevelForTraitXp(hero, DefaultTraits.Honor, xp - 50, out var level, out xp);
        honorXp.Remove(controllerId);
        hero.SetTraitLevel(DefaultTraits.Honor, level);
        network.SendAll(new NetworkLadysKnightOutHonor(heroId, controllerId, xp, level));
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestLadysKnightOutHonorMirror : ICommand
{
    [ProtoMember(1)] public readonly string ControllerId;
    [ProtoMember(2)] public readonly int Xp;

    public RequestLadysKnightOutHonorMirror(string controllerId, int xp)
    {
        ControllerId = controllerId;
        Xp = xp;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLadysKnightOutHonor : ICommand
{
    [ProtoMember(1)] public readonly string HeroId;
    [ProtoMember(2)] public readonly string ControllerId;
    [ProtoMember(3)] public readonly int Xp;
    [ProtoMember(4)] public readonly int Level;

    public NetworkLadysKnightOutHonor(string heroId, string controllerId, int xp, int level)
    {
        HeroId = heroId;
        ControllerId = controllerId;
        Xp = xp;
        Level = level;
    }
}

internal sealed class LadysKnightOutHonorHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;
    private readonly IControllerIdProvider controller;

    public LadysKnightOutHonorHandler(IMessageBroker broker, IObjectManager objects, IControllerIdProvider controller)
    {
        this.broker = broker;
        this.objects = objects;
        this.controller = controller;
        broker.Subscribe<NetworkLadysKnightOutHonor>(HandleHonor);
        broker.Subscribe<RequestLadysKnightOutHonorMirror>(HandleMirror);
    }

    public void Dispose()
    {
        broker.Unsubscribe<NetworkLadysKnightOutHonor>(HandleHonor);
        broker.Unsubscribe<RequestLadysKnightOutHonorMirror>(HandleMirror);
    }

    private void HandleMirror(MessagePayload<RequestLadysKnightOutHonorMirror> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            if (string.IsNullOrEmpty(payload.What.ControllerId)) return;
            if (ContainerProvider.TryResolve<ILadysKnightOutHonorProgress>(out var progress))
                progress.MirrorServerHonor(payload.What.ControllerId, payload.What.Xp);
        }, context: nameof(LadysKnightOutHonorHandler));
    }

    private void HandleHonor(MessagePayload<NetworkLadysKnightOutHonor> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            var data = payload.What;
            if (!objects.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;
            using (new AllowedThread()) hero.SetTraitLevel(DefaultTraits.Honor, data.Level);
            if (data.ControllerId == controller.ControllerId)
                Campaign.Current.PlayerTraitDeveloper?.SetPropertyValue(DefaultTraits.Honor, data.Xp);
        }, context: nameof(LadysKnightOutHonorHandler));
    }
}
