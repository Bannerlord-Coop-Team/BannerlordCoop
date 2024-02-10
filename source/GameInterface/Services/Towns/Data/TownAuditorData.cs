using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Settlements.Town;

namespace GameInterface.Services.Towns.Data;

/// <summary>
/// Get all the sync values of a town
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class TownAuditorData
{
    [ProtoMember(1)]
    public string TownStringId { get; }
    [ProtoMember(2)]
    public string Name { get; }
    [ProtoMember(3)]
    public string Governor { get; }
    [ProtoMember(4)]
    public string LastCapturedBy { get; }
    [ProtoMember(5)]
    public float Prosperity { get; }
    [ProtoMember(6)]
    public float Loyalty { get; }
    [ProtoMember(7)]
    public float Security { get; }
    [ProtoMember(8)]
    public bool InRebelliousState { get; }
    [ProtoMember(9)]
    public bool GarrisonAutoRecruitmentIsEnabled { get; }
    [ProtoMember(10)]
    public float FoodStocks { get; }
    [ProtoMember(11)]
    public int TradeTaxAccumulated { get; }
    [ProtoMember(12)]
    public SellLogData[] SellLogList { get; }


    public TownAuditorData(
        string townStringId,
        string name,
        string governor,
        string lastCapturedBy,
        float prosperity,
        float loyalty,
        float security,
        bool inRebelliousState,
        bool garrisonAutoRecruitmentIsEnabled,
        float foodStocks,
        int tradeTaxAccumulated,
        IEnumerable<Town.SellLog> sellLogList)
    {
        TownStringId = townStringId;
        Name = name;
        Governor = governor;
        LastCapturedBy = lastCapturedBy;
        Prosperity = prosperity;
        Loyalty = loyalty;
        Security = security;
        InRebelliousState = inRebelliousState;
        GarrisonAutoRecruitmentIsEnabled = garrisonAutoRecruitmentIsEnabled;
        FoodStocks = foodStocks;
        TradeTaxAccumulated = tradeTaxAccumulated;
        SellLogList = sellLogList.Select(sellLog => new SellLogData(sellLog.Number, sellLog.Category.StringId)).ToArray();
    }
}
