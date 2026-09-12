using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkTacticalUnitSymbolsVisibilityChanged : ICommand
{
    [ProtoMember(1)]
    public readonly bool HideTacticalUnitSymbols;

    public NetworkTacticalUnitSymbolsVisibilityChanged(bool hideTacticalUnitSymbols)
    {
        HideTacticalUnitSymbols = hideTacticalUnitSymbols;
    }
}
