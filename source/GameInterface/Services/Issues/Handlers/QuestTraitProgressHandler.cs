using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Issues.Handlers;

internal sealed class QuestTraitProgressHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly HashSet<Hero> enrolled = new();
    private bool applying;

    public QuestTraitProgressHandler(IMessageBroker broker, INetwork network, IObjectManager objects, IPlayerManager players)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.players = players;
        broker.Subscribe<RequestQuestTraitProgress>(Handle_Initialize);
        broker.Subscribe<RequestQuestTraitXpChange>(Handle_Change);
        broker.Subscribe<NetworkQuestTraitProgress>(Handle_Progress);
    }

    public void Dispose()
    {
        broker.Unsubscribe<RequestQuestTraitProgress>(Handle_Initialize);
        broker.Unsubscribe<RequestQuestTraitXpChange>(Handle_Change);
        broker.Unsubscribe<NetworkQuestTraitProgress>(Handle_Progress);
    }

    internal bool HasProgress(Hero hero) => hero != null && enrolled.Contains(hero);

    internal void Begin()
    {
        if (ModInformation.IsServer) return;
        var hero = Hero.MainHero;
        if (!objects.TryGetIdWithLogging(hero, out var id)) return;
        var values = Capture(Campaign.Current.PlayerTraitDeveloper);
        if (!HasProgress(hero)) Store(hero, values);
        network.SendAll(new RequestQuestTraitProgress(id, values));
    }

    internal void SyncData(IDataStore dataStore)
    {
        var saved = dataStore.IsSaving ? enrolled.ToList() : null;
        dataStore.SyncData("_coop_quest_trait_progress_players", ref saved);
        if (!dataStore.IsLoading) return;
        enrolled.Clear();
        foreach (var hero in saved ?? new List<Hero>())
            if (hero != null) enrolled.Add(hero);
    }

    internal void RestoreLocalProgress()
    {
        if (ModInformation.IsClient && HasProgress(Hero.MainHero))
            Campaign.Current.PlayerTraitDeveloper = GetProgress(Hero.MainHero);
    }

    private PropertyOwner<PropertyObject> GetProgress(Hero hero)
    {
        var registry = GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress;
        if (!registry.TryGet(hero, out var progress))
        {
            progress = new PropertyOwner<PropertyObject>();
            registry.Set(hero, progress);
        }
        return progress;
    }

    private static Dictionary<string, int> Capture(PropertyOwner<PropertyObject> progress) =>
        progress.GetProperties().OfType<TraitObject>().ToDictionary(trait => trait.StringId, progress.GetPropertyValue);

    internal void Store(Hero hero, Dictionary<string, int> values)
    {
        var progress = GetProgress(hero);
        progress.ClearAllProperty();
        foreach (var entry in values)
        {
            var trait = MBObjectManager.Instance.GetObject<TraitObject>(entry.Key);
            if (trait != null) progress.SetPropertyValue(trait, entry.Value);
        }
        enrolled.Add(hero);
    }

    internal bool TryApply(TraitObject trait, int amount, ActionNotes context, Hero referenceHero)
    {
        var hero = Hero.MainHero;
        if (applying || !HasProgress(hero)) return false;
        if (!objects.TryGetIdWithLogging(hero, out var heroId)) return true;
        string referenceId = null;
        if (referenceHero != null && !objects.TryGetIdWithLogging(referenceHero, out referenceId)) return true;
        if (ModInformation.IsClient)
        {
            network.SendAll(new RequestQuestTraitXpChange(heroId, trait.StringId, amount, (int)context, referenceId));
            return true;
        }

        var previous = Campaign.Current.PlayerTraitDeveloper;
        var oldLevel = hero.GetTraitLevel(trait);
        Campaign.Current.PlayerTraitDeveloper = GetProgress(hero);
        applying = true;
        try
        {
            TraitLevelingHelper.AddPlayerTraitXPAndLogEntry(trait, amount, context, referenceHero);
        }
        finally
        {
            applying = false;
            Campaign.Current.PlayerTraitDeveloper = previous;
        }
        Publish(hero, trait, oldLevel, amount, context, referenceId);
        return true;
    }

    internal void Publish(Hero hero, TraitObject trait, int oldLevel, int amount = 0,
        ActionNotes context = default, string referenceId = null)
    {
        if (ModInformation.IsClient || !HasProgress(hero) || !objects.TryGetIdWithLogging(hero, out var id)) return;
        network.SendAll(new NetworkQuestTraitProgress(id, Capture(GetProgress(hero)), trait?.StringId,
            oldLevel, trait == null ? 0 : hero.GetTraitLevel(trait), amount, (int)context, referenceId));
    }

    private void Handle_Initialize(MessagePayload<RequestQuestTraitProgress> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            if (payload.Who is not NetPeer peer || !players.TryGetPlayer(peer, out var player)
                || player.HeroId != payload.What.HeroId || payload.What.Values == null) return;
            if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return;
            // A repeat accept or reconnect must not replace already authoritative XP with an older copy.
            if (!HasProgress(hero))
            {
                var values = new Dictionary<string, int>(payload.What.Values);
                if (GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.TryGet(hero, out var previous))
                    foreach (var entry in Capture(previous)) values[entry.Key] = entry.Value;
                Store(hero, values);
            }
            Publish(hero, null, 0);
        });
    }

    private void Handle_Change(MessagePayload<RequestQuestTraitXpChange> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            var data = payload.What;
            if (payload.Who is not NetPeer peer || !players.TryGetPlayer(peer, out var player)
                || player.HeroId != data.HeroId || !Enum.IsDefined(typeof(ActionNotes), data.Context)) return;
            if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) || !HasProgress(hero)) return;
            if (!objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;
            var trait = MBObjectManager.Instance.GetObject<TraitObject>(data.TraitId);
            if (trait == null) return;
            Hero reference = null;
            if (data.ReferenceHeroId != null && !objects.TryGetObjectWithLogging(data.ReferenceHeroId, out reference)) return;
            using (new MainHeroSubstitutionScope(hero, party))
                TryApply(trait, data.Amount, (ActionNotes)data.Context, reference);
        });
    }

    private void Handle_Progress(MessagePayload<NetworkQuestTraitProgress> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            var data = payload.What;
            if (!objects.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;
            Store(hero, data.Values);
            var trait = data.TraitId == null ? null : MBObjectManager.Instance.GetObject<TraitObject>(data.TraitId);
            using (new AllowedThread())
            {
                if (trait != null) hero.SetTraitLevel(trait, data.Level);
                if (hero != Hero.MainHero) return;
                RestoreLocalProgress();
                if (trait == null) return;
                if (data.OldLevel != data.Level) CampaignEventDispatcher.Instance.OnPlayerTraitChanged(trait, data.OldLevel);
                if (Math.Abs((long)data.Amount) < 10) return;
                Hero reference = null;
                if (data.ReferenceHeroId != null && !objects.TryGetObjectWithLogging(data.ReferenceHeroId, out reference)) return;
                LogEntry.AddLogEntry(new PlayerReputationChangesLogEntry(trait, reference, (ActionNotes)data.Context));
            }
        });
    }
}
