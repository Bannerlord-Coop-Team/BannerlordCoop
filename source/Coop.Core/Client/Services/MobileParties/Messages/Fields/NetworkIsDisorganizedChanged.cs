using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _isDisorganized
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkIsDisorganizedChanged(bool IsDisorganized, string MobilePartyId) : ICommand
{
    public bool IsDisorganized { get; } = IsDisorganized;
    public string MobilePartyId { get; } = MobilePartyId;
}