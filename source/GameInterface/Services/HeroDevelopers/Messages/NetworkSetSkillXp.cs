using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.HeroDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkSetSkillXpServer : ICommand
{
    [ProtoMember(1)]
    public string HeroId;

    [ProtoMember(2)]
    public string SkillObjectId;

    [ProtoMember(3)]
    public float Value;

    public NetworkSetSkillXpServer(
        string heroId,
        string skillObjectId,
        float value)
    {
        HeroId = heroId;
        SkillObjectId = skillObjectId;
        Value = value;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkSetSkillXpClients : ICommand
{
    [ProtoMember(1)]
    public string HeroId;

    [ProtoMember(2)]
    public string SkillObjectId;

    [ProtoMember(3)]
    public float Value;

    public NetworkSetSkillXpClients(NetworkSetSkillXpServer cloneObject)
    {
        HeroId = cloneObject.HeroId;
        SkillObjectId = cloneObject.SkillObjectId;
        Value = cloneObject.Value;
    }
}