using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.BattleWreckages.Messages;

// TODO: Add the looted troops, items, gold and consequence explanations once the server stores them
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkShowWreckageInvestigationResults : ICommand
{
}
