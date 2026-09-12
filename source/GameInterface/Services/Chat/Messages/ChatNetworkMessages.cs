using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.Chat.Messages;

public enum ChatChannel
{
    Global = 0,
    Direct = 1,
    System = 2,
}

public static class ChatMessageLimits
{
    public const int MaxMessageLength = 256;
}

/// <summary>Requests the controller ids that currently have a live server connection.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRequestChatParticipants : ICommand
{
}

/// <summary>Replaces the client's selectable direct-message recipients with live session members.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkChatParticipants : IEvent
{
    [ProtoMember(1)]
    public readonly string[] ControllerIds;

    public NetworkChatParticipants(string[] controllerIds)
    {
        ControllerIds = controllerIds ?? Array.Empty<string>();
    }
}

/// <summary>
/// Requests a chat send. Sender identity is deliberately omitted and is resolved from the
/// originating peer by the server.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSendChatMessage : ICommand
{
    [ProtoMember(1)]
    public readonly ChatChannel Channel;

    [ProtoMember(2)]
    public readonly string RecipientControllerId;

    [ProtoMember(3)]
    public readonly string Text;

    public NetworkSendChatMessage(ChatChannel channel, string recipientControllerId, string text)
    {
        Channel = channel;
        RecipientControllerId = recipientControllerId;
        Text = text;
    }
}

/// <summary>A server-authored chat line delivered to the clients allowed to see it.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkChatMessage : IEvent
{
    [ProtoMember(1)]
    public readonly ChatChannel Channel;

    [ProtoMember(2)]
    public readonly string SenderControllerId;

    [ProtoMember(3)]
    public readonly string SenderName;

    [ProtoMember(4)]
    public readonly string RecipientControllerId;

    [ProtoMember(5)]
    public readonly string RecipientName;

    [ProtoMember(6)]
    public readonly string Text;

    public NetworkChatMessage(
        ChatChannel channel,
        string senderControllerId,
        string senderName,
        string recipientControllerId,
        string recipientName,
        string text)
    {
        Channel = channel;
        SenderControllerId = senderControllerId;
        SenderName = senderName;
        RecipientControllerId = recipientControllerId;
        RecipientName = recipientName;
        Text = text;
    }
}
