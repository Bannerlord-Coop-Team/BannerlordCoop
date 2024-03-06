using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.CampaignService.Messages;
public record ChangeCampaignTime : ICommand
{
    public long NumTicks { get; }
    public long DeltaTime { get; }

    public ChangeCampaignTime(long numTicks, long deltaTime)
    {
        NumTicks = numTicks;
        DeltaTime = deltaTime;
    }
}
