using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.HeroDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkRawXpGainServer : ICommand
{
    [ProtoMember(1)]
    public string HeroId;

    [ProtoMember(2)]
    public float RawXp;

    [ProtoMember(3)]
    public bool ShouldNotify;

    public NetworkRawXpGainServer(
        string heroId,
        float rawXp,
        bool shouldNotify)
    {
        HeroId = heroId;
        RawXp = rawXp;
        ShouldNotify = shouldNotify;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkRawXpGainClients : ICommand
{
    [ProtoMember(1)]
    public string HeroId;

    [ProtoMember(2)]
    public float RawXp;

    [ProtoMember(3)]
    public bool ShouldNotify;

    public NetworkRawXpGainClients(NetworkRawXpGainServer cloneObject)
    {
        HeroId = cloneObject.HeroId;
        RawXp = cloneObject.RawXp;
        ShouldNotify = cloneObject.ShouldNotify;
    }
}