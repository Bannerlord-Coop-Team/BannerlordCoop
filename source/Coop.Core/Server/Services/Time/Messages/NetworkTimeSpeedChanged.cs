﻿using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using ProtoBuf;

namespace Coop.Core.Server.Services.Time.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkTimeSpeedChanged : IEvent
    {
        [ProtoMember(1)]
        public TimeControlEnum NewControlMode { get; }

        public NetworkTimeSpeedChanged(TimeControlEnum newControlMode)
        {
            NewControlMode = newControlMode;
        }
    }
}