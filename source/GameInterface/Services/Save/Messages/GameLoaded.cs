﻿using Common.Messaging;

namespace GameInterface.Services.Heroes.Messages;

public record GameLoaded : IEvent
{
    public string SaveName { get; }

    public GameLoaded(string saveName)
    {
        SaveName = saveName;
    }
}
