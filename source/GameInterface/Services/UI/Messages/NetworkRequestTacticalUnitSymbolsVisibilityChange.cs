using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestTacticalUnitSymbolsVisibilityChange : ICommand
{
    [ProtoMember(1)]
    public readonly bool HideTacticalUnitSymbols;

    public NetworkRequestTacticalUnitSymbolsVisibilityChange(bool hideTacticalUnitSymbols)
    {
        HideTacticalUnitSymbols = hideTacticalUnitSymbols;
    }
}
