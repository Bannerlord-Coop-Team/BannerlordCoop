using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyIncidentOutcomes : ICommand
{
    [ProtoMember(1)]
    public readonly List<TextObject> Outcomes;

    public NetworkNotifyIncidentOutcomes(List<TextObject> outcomes)
    {
        Outcomes = outcomes;
    }
}
