using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using System;

namespace Coop.Core.Client.Services.Kingdoms;

public interface IPeaceOfferPendingApplier
{
    void Apply(PendingPeaceOfferBaseline[] offers);
}

internal sealed class PeaceOfferPendingApplier : IPeaceOfferPendingApplier
{
    public void Apply(PendingPeaceOfferBaseline[] offers)
    {
        offers ??= Array.Empty<PendingPeaceOfferBaseline>();
        var entries = new (string, string)[offers.Length];
        for (int i = 0; i < offers.Length; i++)
        {
            entries[i] = (offers[i].RequestingKingdomId, offers[i].TargetKingdomId);
        }
        PeaceOfferPendingRegistry.RestoreAll(entries);
    }
}
