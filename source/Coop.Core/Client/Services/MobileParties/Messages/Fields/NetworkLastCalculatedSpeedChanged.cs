using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Command to change the property _lastCalculatedSpeed of a mobile party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkLastCalculatedSpeedChanged : ICommand
{
    [ProtoMember(1)]
    public float LastCalculatedSpeed { get; }
    [ProtoMember(2)]
    public string MobilePartyId { get; }

    public NetworkLastCalculatedSpeedChanged(float lastCalculatedSpeed, string mobilePartyId)
    {
        LastCalculatedSpeed = lastCalculatedSpeed;
        MobilePartyId = mobilePartyId;
    }
}