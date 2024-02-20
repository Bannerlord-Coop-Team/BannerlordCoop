using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Utils.AutoSync.Example;
public class DataChanged : IEvent
{
    public DataChanged(MessageData data)
    {
        Data = data;
    }

    public MessageData Data { get; }
}
