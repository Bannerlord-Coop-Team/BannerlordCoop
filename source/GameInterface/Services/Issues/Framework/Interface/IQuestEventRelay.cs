using System;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>
/// The server's quests have no event listeners and clients do not run every world action, so the server runs
/// each registered quest's own vanilla listener for a world event, as the player that owns the quest.
/// </summary>
public interface IQuestEventRelay
{
    // Server: runs the listener with that name and parameters on every running registered quest that has one
    void Relay(string listenerName, Type[] parameterTypes, params object[] arguments);
}
