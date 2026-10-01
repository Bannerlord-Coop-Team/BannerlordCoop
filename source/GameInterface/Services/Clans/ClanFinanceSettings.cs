using ProtoBuf;

namespace GameInterface.Services.Clans;

[ProtoContract(SkipConstructor = true)]
public class ClanFinanceSettings
{
    [ProtoMember(1)]
    public string ClanId { get; }
    [ProtoMember(2)]
    public string LeaderId { get; }
    [ProtoMember(3)]
    public int DailyPayment { get; }

    public ClanFinanceSettings(string clanId, string leaderId, int dailyPayment)
    {
        ClanId = clanId;
        LeaderId = leaderId;
        DailyPayment = dailyPayment;
    }
}
