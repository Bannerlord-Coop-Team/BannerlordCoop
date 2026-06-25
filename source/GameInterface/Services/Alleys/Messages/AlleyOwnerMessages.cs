using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#nullable enable

namespace GameInterface.Services.Alleys.Messages;

/// <summary>
/// Local event published on the server when an <see cref="Alley"/>'s owner changes
/// (via <c>Alley.SetOwner</c>). Carries the live alley and the new owner so the owner
/// change can be turned into a networked <see cref="ChangeAlleyOwner"/> broadcast.
/// </summary>
public readonly struct AlleyOwnerChanged : IEvent
{
    public readonly Alley Alley;
    public readonly Hero? NewOwner;

    public AlleyOwnerChanged(Alley alley, Hero? newOwner)
    {
        Alley = alley;
        NewOwner = newOwner;
    }
}

/// <summary>
/// Networked command broadcast from the server so every client replays
/// <c>Alley.SetOwner</c> for the alley, reproducing the owner, the derived
/// <c>State</c> (per-client, relative to that client's main hero) and the
/// owner's <c>OwnedAlleys</c> list in one call.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct ChangeAlleyOwner : ICommand
{
    [ProtoMember(1)]
    public readonly string AlleyId;

    [ProtoMember(2)]
    public readonly string? NewOwnerId;

    public ChangeAlleyOwner(string alleyId, string? newOwnerId)
    {
        AlleyId = alleyId;
        NewOwnerId = newOwnerId;
    }
}
