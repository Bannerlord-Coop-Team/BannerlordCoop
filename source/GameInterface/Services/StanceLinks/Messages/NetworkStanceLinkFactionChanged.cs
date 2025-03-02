using System;
using System.Collections.Generic;
using System.Text;
using Common.Messaging;
using TaleWorlds.CampaignSystem;
using ProtoBuf;
using GameInterface.Services.Stances.Data;

namespace GameInterface.Services.StanceLinks.Messages;
    
[ProtoContract(SkipConstructor = true)]
internal class NetworkStanceLinkFactionChanged : ICommand
{
    public NetworkStanceLinkFactionChanged(StanceLinkFactionChangedData data)
    {
        Data = data;
    }

    [ProtoMember(1)]
    public StanceLinkFactionChangedData Data { get; }
}
