using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Messages;

// --- Local event ---

/// <summary>
/// Published on the server (from <see cref="Patches.HeadmanNeedsGrainPriceCachePatches"/>) immediately after
/// a genuine server-side <c>HeadmanNeedsGrainIssueBehavior.CacheGrainPrice()</c> recompute. Carries the
/// freshly-cached price so <see cref="Handlers.HeadmanNeedsGrainPriceHandler"/> can broadcast it to every
/// connected client.
/// </summary>
public readonly struct HeadmanGrainPriceCached : IEvent
{
    public readonly int Price;

    public HeadmanGrainPriceCached(int price)
    {
        Price = price;
    }
}

// --- Networked message ---

/// <summary>
/// Server -> all clients: the authoritative <c>_averageGrainPriceInCalradia</c>. A client force-writes this
/// directly into its own local <c>HeadmanNeedsGrainIssueBehavior</c> singleton instance instead of
/// independently recomputing - see <see cref="Patches.HeadmanNeedsGrainPriceCachePatches"/>, which blocks a
/// client's own <c>CacheGrainPrice()</c> from running the real computation at all.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeadmanGrainPriceUpdated : ICommand
{
    [ProtoMember(1)]
    public readonly int Price;

    public NetworkHeadmanGrainPriceUpdated(int price)
    {
        Price = price;
    }
}
