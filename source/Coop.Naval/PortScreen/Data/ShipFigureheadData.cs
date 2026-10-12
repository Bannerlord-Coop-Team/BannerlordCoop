using ProtoBuf;

namespace Coop.Naval.PortScreen.Data;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ShipFigureheadData
{
    [ProtoMember(1)]
    public readonly uint ShipHandle;

    [ProtoMember(2)]
    public readonly uint FigureheadHandle;

    public ShipFigureheadData(
        uint shipHandle,
        uint figureheadHandle)
    {
        ShipHandle = shipHandle;
        FigureheadHandle = figureheadHandle;
    }
}
