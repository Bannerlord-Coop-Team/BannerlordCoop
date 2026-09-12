using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;

namespace Coop.Core.Server.Services.Kingdoms;

public interface IAllianceOfferPendingCapturer
{
    PendingAllianceOfferBaseline[] Capture();
}

internal sealed class AllianceOfferPendingCapturer : IAllianceOfferPendingCapturer
{
    public PendingAllianceOfferBaseline[] Capture()
    {
        var pending = AllianceOfferPendingRegistry.Snapshot();
        var result = new PendingAllianceOfferBaseline[pending.Length];
        for (int i = 0; i < pending.Length; i++)
        {
            result[i] = new PendingAllianceOfferBaseline(pending[i].RequestingKingdomId, pending[i].TargetKingdomId);
        }
        return result;
    }
}