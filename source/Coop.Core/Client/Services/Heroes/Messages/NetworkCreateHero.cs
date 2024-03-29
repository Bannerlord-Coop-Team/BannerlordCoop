﻿using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages;

/// <summary>
/// Command to create a new hero.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkCreateHero : ICommand
{
    public NetworkCreateHero(HeroCreationData heroData)
    {
        Data = heroData;
    }

    [ProtoMember(1)]
    public HeroCreationData Data { get; }
}