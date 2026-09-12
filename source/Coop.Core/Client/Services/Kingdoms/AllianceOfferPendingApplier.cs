using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using System;

namespace Coop.Core.Client.Services.Kingdoms;

public interface IAllianceOfferPendingApplier
{
    void Apply(PendingAllianceOfferBaseline[] offers);
}

internal sealed class AllianceOfferPendingApplier : IAllianceOfferPendingApplier
{
    public void Apply(PendingAllianceOfferBaseline[] offers)
    {
        offers ??= Array.Empty<PendingAllianceOfferBaseline>();
        var entries = new (string, string)[offers.Length];
        for (int i = 0; i < offers.Length; i++)
        {
            entries[i] = (offers[i].RequestingKingdomId, offers[i].TargetKingdomId);
        }
        AllianceOfferPendingRegistry.RestoreAll(entries);
    }
}