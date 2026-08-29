using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

public readonly struct ClientSelectHeir : IEvent
{
    public readonly Hero PlayerVictim;
    public readonly Dictionary<Hero, int> HeirApparents;

    public ClientSelectHeir(
        Hero playerVictim,
        Dictionary<Hero, int> heirApparents)
    {
        PlayerVictim = playerVictim;
        HeirApparents = heirApparents;
    }
}