using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Kingdoms.Messages;
internal class KingdomCreated : IEvent
{
    public KingdomCreated(Kingdom kingdom)
    {
        Kingdom = kingdom;
    }

    public Kingdom Kingdom { get; }
}
