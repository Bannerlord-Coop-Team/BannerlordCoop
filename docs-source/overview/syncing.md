## Using Coop Managed

As an example let's sync some of the Hero class.

```CS
class HeroSync : CoopManaged<HeroSync, Hero>
{
    static HeroSync()
    {
        When(GameLoop)
            .Calls(Setter(nameof(Hero.HasMet)),
                    Method(nameof(Hero.ChangeState)),
                    Method(typeof(AddCompanionAction), nameof(AddCompanionAction.Apply)))
            .Broadcast(() => CoopClient.Instance.Synchronization)
            .DelegateTo(IsServer);

        ApplyStaticPatches();
        AutoWrapAllInstances(c => new HeroSync(c));
    }

    private static ECallPropagation IsServer(IPendingMethodCall call)
    {
        if(Coop.IsServer || (Hero)call.Instance == Hero.MainHero)
        {
            return ECallPropagation.CallOriginal;
        }
        else
        {
            return ECallPropagation.Skip;
        }
    }

    public HeroSync([NotNull] Hero instance) : base(instance)
    {
    }
}
```

The following must exist in a **Static** constructor.

## When
Defines when to process a call. The current default Conditions for this are GameLoop (from this clients game) or RemoteAuthority (from another clients game).

## Calls
Defines which methods/setters are to be patched (can be a list of Methods/Setters). Defining Setter or Method works very similar to reflection.

## Broadcast
Broadcasts the call to all clients if DelegatesTo allows the call.

## DelegateTo
Allows or disallows the call. So in this case we only want the main hero of each client to allow the call or any of the heros on the server.

Note - DelegateTo is not required, if possible simplify to Execute or Skip.

## ApplyStaticPatches
Patches any static Methods/Setters, this currently has to be done manually.

## AutoWrapAllInstances
Wraps all instances in a patch for non static Methods/Setters, this currently has to be done manually. AutoWrapAllInstances manages all creation and destruction of instances.