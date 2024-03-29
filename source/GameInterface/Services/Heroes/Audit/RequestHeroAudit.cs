﻿using Common.Messaging;
using ProtoBuf;


namespace GameInterface.Services.Heroes.Audit;

[ProtoContract(SkipConstructor = true)]
internal record RequestHeroAudit : ICommand
{
    [ProtoMember(1)]
    public HeroAuditData[] Data { get; }

    public RequestHeroAudit(HeroAuditData[] data)
    {
        Data = data;
    }
}
