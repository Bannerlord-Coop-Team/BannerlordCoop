#if DEBUG
using Autofac;
using Common;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.Commands;

internal static class CaptureOutcomeFixtureCommands
{
    private static CaptureOutcomeFixture fixture;

    private sealed class CaptureOutcomeFixture
    {
        public Campaign Campaign;
        public Hero Hero;
        public string HeroId;
        public MapEvent MapEvent;
        public string MapEventId;
        public bool Consumed;
        public float OriginalChanceTotal;
        public float GuaranteedChanceTotal;
    }

    [CommandLineArgumentFunction("capture_outcome_begin", "coop.debug.mapevent")]
    public static string Begin(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 1)
            return "Usage: coop.debug.mapevent.capture_outcome_begin <heroId>";
        if (fixture != null)
            return "A capture-outcome fixture is already pending reset.";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            return "Unable to resolve ObjectManager.";
        if (!objectManager.TryGetObject(args[0], out Hero hero))
            return $"Hero with id {args[0]} was not found.";
        if (hero.IsPrisoner)
            return $"Hero {args[0]} is already a prisoner.";

        MapEvent mapEvent = hero.PartyBelongedTo?.MapEvent;
        if (mapEvent == null)
            return $"Hero {args[0]} is not in a map event.";
        if (!objectManager.TryGetId(mapEvent, out string mapEventId))
            return "The hero's map event is not registered.";

        fixture = new CaptureOutcomeFixture
        {
            Campaign = Campaign.Current,
            Hero = hero,
            HeroId = args[0],
            MapEvent = mapEvent,
            MapEventId = mapEventId,
        };

        return FormatState("armed", fixture);
    }

    [CommandLineArgumentFunction("capture_outcome_status", "coop.debug.mapevent")]
    public static string Status(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.capture_outcome_status";
        if (fixture == null)
            return "CAPTURE_OUTCOME_FIXTURE state=none";

        return FormatState(fixture.Consumed ? "consumed" : "armed", fixture);
    }

    [CommandLineArgumentFunction("capture_outcome_reset", "coop.debug.mapevent")]
    public static string Reset(List<string> args)
    {
        if (!ModInformation.IsServer)
            return "Run this command on the server.";
        if (args.Count != 0)
            return "Usage: coop.debug.mapevent.capture_outcome_reset";
        if (fixture == null)
            return "CAPTURE_OUTCOME_FIXTURE_RESET state=none";

        CaptureOutcomeFixture activeFixture = fixture;
        fixture = null;
        return "CAPTURE_OUTCOME_FIXTURE_RESET " +
               FormatState(activeFixture.Consumed ? "consumed" : "armed", activeFixture);
    }

    internal static MBList<KeyValuePair<MapEventParty, float>> ApplyGuaranteedCapture(
        Hero hero,
        MapEvent mapEvent,
        MBList<KeyValuePair<MapEventParty, float>> captureChances)
    {
        CaptureOutcomeFixture activeFixture = fixture;
        if (activeFixture == null ||
            activeFixture.Consumed ||
            activeFixture.Campaign != Campaign.Current ||
            activeFixture.Hero != hero ||
            activeFixture.MapEvent != mapEvent ||
            captureChances.Count == 0)
        {
            return captureChances;
        }

        float total = 0f;
        for (int i = 0; i < captureChances.Count; i++)
            total += captureChances[i].Value;
        if (total <= 0f)
            return captureChances;

        var guaranteedChances = new MBList<KeyValuePair<MapEventParty, float>>();
        float remaining = 1f;
        for (int i = 0; i < captureChances.Count; i++)
        {
            float chance = i == captureChances.Count - 1
                ? remaining
                : TaleWorlds.Library.MathF.Min(remaining, captureChances[i].Value / total);
            guaranteedChances.Add(new KeyValuePair<MapEventParty, float>(
                captureChances[i].Key,
                chance));
            remaining -= chance;
        }

        activeFixture.Consumed = true;
        activeFixture.OriginalChanceTotal = total;
        activeFixture.GuaranteedChanceTotal = 1f - remaining;
        return guaranteedChances;
    }

    private static string FormatState(string state, CaptureOutcomeFixture activeFixture)
    {
        return $"CAPTURE_OUTCOME_FIXTURE state={state} " +
               $"hero={activeFixture.HeroId} mapEvent={activeFixture.MapEventId} " +
               $"originalChanceTotal={activeFixture.OriginalChanceTotal:R} " +
               $"guaranteedChanceTotal={activeFixture.GuaranteedChanceTotal:R}";
    }
}
#endif
