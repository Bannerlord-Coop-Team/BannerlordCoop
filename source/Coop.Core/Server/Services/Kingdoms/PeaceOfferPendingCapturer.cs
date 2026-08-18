using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;

namespace Coop.Core.Server.Services.Kingdoms;

public interface IPeaceOfferPendingCapturer
{
    PendingPeaceOfferBaseline[] Capture();
}
internal sealed class PeaceOfferPendingCapturer : IPeaceOfferPendingCapturer
{
    public PendingPeaceOfferBaseline[] Capture()
    {
        var pending = PeaceOfferPendingRegistry.Snapshot();
        var result = new PendingPeaceOfferBaseline[pending.Length];
        for (int i = 0; i < pending.Length; i++)
        {
            result[i] = new PendingPeaceOfferBaseline(pending[i].RequestingKingdomId, pending[i].TargetKingdomId);
        }
        return result;
    }
}
