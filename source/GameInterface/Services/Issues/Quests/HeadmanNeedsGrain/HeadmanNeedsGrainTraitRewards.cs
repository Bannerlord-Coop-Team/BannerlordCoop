using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

internal interface IHeadmanNeedsGrainTraitRewards : IGameAbstraction
{
    void Apply(TraitObject trait, int amount, Hero giver, ActionNotes note);
    void SyncData(IDataStore dataStore);
    void RequestSeed();
    void Seed(Hero owner, Dictionary<string, int> values);
}

internal class HeadmanNeedsGrainTraitRewards : IHeadmanNeedsGrainTraitRewards
{
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private Dictionary<Hero, Dictionary<string, int>> progress = new Dictionary<Hero, Dictionary<string, int>>();

    public HeadmanNeedsGrainTraitRewards(INetwork network, IObjectManager objectManager)
    {
        this.network = network;
        this.objectManager = objectManager;
    }

    public void Apply(TraitObject trait, int amount, Hero giver, ActionNotes note)
    {
        if (ModInformation.IsClient) return;
        var owner = Hero.MainHero;
        if (!objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;
        if (!objectManager.TryGetIdWithLogging(giver, out var giverId)) return;

        if (!progress.TryGetValue(owner, out var values))
        {
            values = new Dictionary<string, int>();
            progress.Add(owner, values);
        }

        var model = Campaign.Current.Models.CharacterDevelopmentModel;
        if (!values.TryGetValue(trait.StringId, out var xp))
            xp = model.GetTraitXpRequiredForTraitLevel(trait, owner.GetTraitLevel(trait));

        model.GetTraitLevelForTraitXp(owner, trait, xp + amount, out var level, out var remainingXp);
        values[trait.StringId] = remainingXp;
        owner.SetTraitLevel(trait, level);
        network.SendAll(new NetworkHeadmanNeedsGrainTraitReward(ownerId, giverId, trait.StringId, remainingXp, level, (int)note));
    }

    public void RequestSeed()
    {
        if (ModInformation.IsServer || Campaign.Current.PlayerTraitDeveloper == null) return;
        network.SendAll(new RequestHeadmanNeedsGrainTraitSeed(TraitObject.All.ToDictionary(
            trait => trait.StringId, trait => Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(trait))));
    }

    public void Seed(Hero owner, Dictionary<string, int> values)
    {
        if (ModInformation.IsClient || values == null) return;
        var model = Campaign.Current.Models.CharacterDevelopmentModel;
        var accepted = new Dictionary<string, int>();
        foreach (var value in values)
        {
            var trait = MBObjectManager.Instance.GetObject<TraitObject>(value.Key);
            if (trait == null) continue;
            model.GetTraitLevelForTraitXp(owner, trait, value.Value, out var level, out _);
            if (level == owner.GetTraitLevel(trait)) accepted[value.Key] = value.Value;
        }
        progress[owner] = accepted;
    }

    public void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_coop_grain_trait_progress", ref progress);
        if (progress == null) progress = new Dictionary<Hero, Dictionary<string, int>>();
        if (!dataStore.IsLoading || ModInformation.IsServer) return;
        if (!progress.TryGetValue(Hero.MainHero, out var values)) return;
        foreach (var value in values)
        {
            var trait = MBObjectManager.Instance.GetObject<TraitObject>(value.Key);
            if (trait != null) Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(trait, value.Value);
        }
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestHeadmanNeedsGrainTraitSeed : ICommand
{
    [ProtoMember(1)] public readonly Dictionary<string, int> Values;
    public RequestHeadmanNeedsGrainTraitSeed(Dictionary<string, int> values) => Values = values;
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHeadmanNeedsGrainTraitReward : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string OwnerId;
    [ProtoMember(2)] public readonly string GiverId;
    [ProtoMember(3)] public readonly string TraitId;
    [ProtoMember(4)] public readonly int Xp;
    [ProtoMember(5)] public readonly int Level;
    [ProtoMember(6)] public readonly int Note;

    public NetworkHeadmanNeedsGrainTraitReward(string ownerId, string giverId, string traitId, int xp, int level, int note)
    {
        OwnerId = ownerId;
        GiverId = giverId;
        TraitId = traitId;
        Xp = xp;
        Level = level;
        Note = note;
    }
}

internal class HeadmanNeedsGrainTraitRewardHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager players;
    private readonly IHeadmanNeedsGrainTraitRewards rewards;

    public HeadmanNeedsGrainTraitRewardHandler(IMessageBroker broker, IObjectManager objectManager,
        IPlayerManager players, IHeadmanNeedsGrainTraitRewards rewards)
    {
        this.broker = broker;
        this.objectManager = objectManager;
        this.players = players;
        this.rewards = rewards;
        broker.Subscribe<NetworkHeadmanNeedsGrainTraitReward>(Handle);
        broker.Subscribe<RequestHeadmanNeedsGrainTraitSeed>(Seed);
    }

    public void Dispose()
    {
        broker.Unsubscribe<NetworkHeadmanNeedsGrainTraitReward>(Handle);
        broker.Unsubscribe<RequestHeadmanNeedsGrainTraitSeed>(Seed);
    }

    private void Seed(MessagePayload<RequestHeadmanNeedsGrainTraitSeed> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer) || !players.TryGetPlayer(peer, out var player)) return;
        GameThread.RunSafe(() =>
        {
            if (objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) rewards.Seed(hero, payload.What.Values);
        }, context: nameof(HeadmanNeedsGrainTraitRewardHandler));
    }

    private void Handle(MessagePayload<NetworkHeadmanNeedsGrainTraitReward> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner) || owner != Hero.MainHero) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            var trait = MBObjectManager.Instance.GetObject<TraitObject>(data.TraitId);
            if (trait == null) return;
            var previousLevel = owner.GetTraitLevel(trait);
            Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(trait, data.Xp);
            using (new Common.Util.AllowedThread()) owner.SetTraitLevel(trait, data.Level);
            if (previousLevel != data.Level) CampaignEventDispatcher.Instance.OnPlayerTraitChanged(trait, previousLevel);
            LogEntry.AddLogEntry(new PlayerReputationChangesLogEntry(trait, giver, (ActionNotes)data.Note));
        }, context: nameof(HeadmanNeedsGrainTraitRewardHandler));
    }
}
