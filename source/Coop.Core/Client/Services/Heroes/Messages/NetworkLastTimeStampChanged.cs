using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Heroes.Messages
{
    /// <summary>
    /// Network Command for LastTimeStampForActivity
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkLastTimeStampChanged : ICommand
    {
        [ProtoMember(1)]
        public int LastTimeStamp { get; }
        [ProtoMember(2)]
        public string HeroId { get; }

        public NetworkLastTimeStampChanged(int lastTimeStamp, string heroId)
        {
            LastTimeStamp = lastTimeStamp;
            HeroId = heroId;
        }
    }
}
