using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.HeroDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkSkillLevelChangeServer : ICommand
{
    [ProtoMember(1)]
    public string HeroId;

    [ProtoMember(2)]
    public string SkillObjectId;

    [ProtoMember(3)]
    public int ChangeAmount;

    [ProtoMember(4)]
    public bool ShouldNotify;

    public NetworkSkillLevelChangeServer(
        string heroId,
        string skillObjectId,
        int changeAmount,
        bool shouldNotify)
    {
        HeroId = heroId;
        SkillObjectId = skillObjectId;
        ChangeAmount = changeAmount;
        ShouldNotify = shouldNotify;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkSkillLevelChangeClients : ICommand
{
    [ProtoMember(1)]
    public string HeroId;

    [ProtoMember(2)]
    public string SkillObjectId;

    [ProtoMember(3)]
    public int ChangeAmount;

    [ProtoMember(4)]
    public bool ShouldNotify;

    public NetworkSkillLevelChangeClients(NetworkSkillLevelChangeServer cloneObject)
    {
        HeroId = cloneObject.HeroId;
        SkillObjectId = cloneObject.SkillObjectId;
        ChangeAmount = cloneObject.ChangeAmount;
        ShouldNotify = cloneObject.ShouldNotify;
    }
}