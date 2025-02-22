using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class ClientCloseRecruitmentVM : ICommand
    {
    }
}
