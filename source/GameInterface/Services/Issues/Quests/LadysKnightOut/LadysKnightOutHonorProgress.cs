using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal interface ILadysKnightOutHonorProgress
{
    void Seed(IssueBase issue, int xp);
    void ApplyPenalty(QuestBase quest);
}

internal sealed class LadysKnightOutHonorProgress : ILadysKnightOutHonorProgress
{
    private readonly Dictionary<Hero, int> honorXp = new Dictionary<Hero, int>();
    private readonly IIssueOwnerResolver owners;
    private readonly IObjectManager objects;
    private readonly INetwork network;

    public LadysKnightOutHonorProgress(IIssueOwnerResolver owners, IObjectManager objects, INetwork network)
    {
        this.owners = owners;
        this.objects = objects;
        this.network = network;
    }

    public void Seed(IssueBase issue, int xp)
    {
        if (owners.TryResolveOwnerHero(issue, out var hero)) honorXp[hero] = xp;
    }

    public void ApplyPenalty(QuestBase quest)
    {
        var issue = quest.QuestGiver.Issue;
        if (ModInformation.IsClient || issue == null || !owners.TryResolveOwnerHero(issue, out var hero) ||
            !owners.TryGetOwnerControllerId(issue, out var controllerId) || !objects.TryGetIdWithLogging(hero, out var heroId)) return;
        var model = Campaign.Current.Models.CharacterDevelopmentModel;
        if (!honorXp.TryGetValue(hero, out var xp)) xp = model.GetTraitXpRequiredForTraitLevel(DefaultTraits.Honor, hero.GetTraitLevel(DefaultTraits.Honor));
        model.GetTraitLevelForTraitXp(hero, DefaultTraits.Honor, xp - 50, out var level, out xp);
        honorXp[hero] = xp;
        hero.SetTraitLevel(DefaultTraits.Honor, level);
        network.SendAll(new NetworkLadysKnightOutHonor { HeroId = heroId, ControllerId = controllerId, Xp = xp, Level = level });
    }
}

[ProtoContract(SkipConstructor = true)]
internal sealed class NetworkLadysKnightOutHonor : ICommand
{
    [ProtoMember(1)] public string HeroId;
    [ProtoMember(2)] public string ControllerId;
    [ProtoMember(3)] public int Xp;
    [ProtoMember(4)] public int Level;
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
    }

    public void Dispose() => broker.Unsubscribe<NetworkLadysKnightOutHonor>(HandleHonor);

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
