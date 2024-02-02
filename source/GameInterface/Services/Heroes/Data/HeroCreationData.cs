using ProtoBuf;

namespace GameInterface.Services.Heroes.Data;
[ProtoContract(SkipConstructor = true)]
public class HeroCreationData
{
    public HeroCreationData(string templateStringId, int age, long birthday, string bornSettlementId)
    {
        TemplateStringId = templateStringId;
        Age = age;
        Birthday = birthday;
        BornSettlementId = bornSettlementId;
    }

    [ProtoMember(1)]
    public string TemplateStringId { get; }
    [ProtoMember(2)]
    public string BornSettlementId { get; }
    [ProtoMember(3)]
    public int Age { get; }
    [ProtoMember(4)]
    public long Birthday { get; }
}
