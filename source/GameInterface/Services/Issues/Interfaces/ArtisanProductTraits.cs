using Common;
using Common.Network;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

internal interface IArtisanProductTraits
{
    IDisposable Enter(Hero owner);
}

internal sealed class ArtisanProductTraits : IArtisanProductTraits
{
    private readonly IObjectManager objects;
    private readonly INetwork network;

    public ArtisanProductTraits(IObjectManager objects, INetwork network)
    {
        this.objects = objects;
        this.network = network;
    }

    public IDisposable Enter(Hero owner)
    {
        if (ModInformation.IsClient) throw new InvalidOperationException("Trait authority requires the server");
        if (!objects.TryGetIdWithLogging(owner, out var ownerId))
            throw new InvalidOperationException("Artisan trait owner is not registered");
        var registry = GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress;
        if (!registry.TryGet(owner, out var progress))
        {
            progress = new PropertyOwner<PropertyObject>();
            registry.Set(owner, progress);
        }
        foreach (var trait in TraitObject.All)
        {
            if (!progress.GetProperties().Contains(trait))
                progress.SetPropertyValue(trait, Campaign.Current.Models.CharacterDevelopmentModel
                    .GetTraitXpRequiredForTraitLevel(trait, owner.GetTraitLevel(trait)));
        }
        return new TraitScope(owner, ownerId, progress, network);
    }

    private sealed class TraitScope : IDisposable
    {
        private readonly Campaign campaign;
        private readonly PropertyOwner<PropertyObject> previous;
        private readonly PropertyOwner<PropertyObject> progress;
        private readonly Hero owner;
        private readonly string ownerId;
        private readonly INetwork network;
        private readonly ArtisanTraitProgress[] before;

        public TraitScope(Hero owner, string ownerId, PropertyOwner<PropertyObject> progress, INetwork network)
        {
            this.owner = owner;
            this.ownerId = ownerId;
            this.progress = progress;
            this.network = network;
            campaign = Campaign.Current;
            previous = campaign.PlayerTraitDeveloper;
            before = Capture();
            campaign.PlayerTraitDeveloper = progress;
        }

        public void Dispose()
        {
            campaign.PlayerTraitDeveloper = previous;
            if (ReferenceEquals(previous, progress)) return;
            var after = Capture();
            if (!before.SequenceEqual(after))
                network.SendAll(new NetworkArtisanTraitProgress(ownerId, after));
        }

        private ArtisanTraitProgress[] Capture()
            => TraitObject.All.Select(trait => new ArtisanTraitProgress(trait.StringId,
                progress.GetPropertyValue(trait), owner.GetTraitLevel(trait))).ToArray();
    }
}
