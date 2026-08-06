namespace GameInterface.Services.Issues.Generic.AcceptMirror;

/// <summary>
/// Empty payload type for accept-mirror strategies that need no captured payload at all - lets
/// <see cref="IAlternativeAcceptMirrorStrategy{TPayload}"/> be instantiated uniformly for both state-only and
/// state-plus-frozen-payload instances.
/// </summary>
public readonly struct Unit
{
    public static readonly Unit Value = default;
}
