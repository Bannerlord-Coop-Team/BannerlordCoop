using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.CampaignServices.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkCampaignTimeChanged : IEvent
{
    [ProtoMember(1)]
    public long NumTicks { get; }
    [ProtoMember(2)]
    public long DeltaTime { get; }

    public NetworkCampaignTimeChanged(long numTicks, long deltaTime)
    {
        NumTicks = numTicks;
        DeltaTime = deltaTime;
    }
}
