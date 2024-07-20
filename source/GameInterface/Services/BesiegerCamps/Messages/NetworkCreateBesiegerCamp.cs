using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.BesiegerCamps.Messages;
internal class NetworkCreateBesiegerCamp : ICommand
{
    public string BesiegerCampId { get; }

    public NetworkCreateBesiegerCamp(string besiegerCampId)
    {
        BesiegerCampId = besiegerCampId;
    }
}
