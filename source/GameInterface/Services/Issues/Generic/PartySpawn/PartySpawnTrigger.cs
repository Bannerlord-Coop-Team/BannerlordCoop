namespace GameInterface.Services.Issues.Generic.PartySpawn;

public abstract record PartySpawnTrigger
{
    public sealed record SpawnMethodWrap(string MethodName) : PartySpawnTrigger;

    public sealed record DialogueConsequence(string ConsequenceMethodName) : PartySpawnTrigger;

    public sealed record EventListener(string ListenerMethodName) : PartySpawnTrigger;
}
