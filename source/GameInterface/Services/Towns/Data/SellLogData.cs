using ProtoBuf;

namespace GameInterface.Services.Towns.Data;

/// <summary>
/// Converts SellLog data into transferable types
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class SellLogData
{
    [ProtoMember(1)]
    public int Number { get; }

    [ProtoMember(2)]
    public string CategoryID { get; }
    public SellLogData(int number, string categoryID)
    {
        Number = number;
        CategoryID = categoryID;
    }
}
