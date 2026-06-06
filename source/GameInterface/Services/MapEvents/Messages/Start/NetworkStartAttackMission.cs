using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract]
internal readonly struct NetworkStartAttackMission : ICommand
{
}
