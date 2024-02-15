using GameInterface.Services.Heroes.Audit;
using GameInterface.Services.MobileParties.Audit;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MobileParties.Commands;

internal class MobilePartyDebugCommand
{
    [CommandLineArgumentFunction("info", "coop.debug.mobileparty")]
    public static string Info(List<string> args)
    {
        if(args.Count < 1)
        {
            return "Usage: coop.debug.mobileparty.info <PartyStringID>";
        }

        MobileParty mobileParty = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);

        if(mobileParty == null )
        {
            return string.Format("ID: '{0}' not found", args[0]);
        }

        Hero owner = mobileParty.Owner;
        var _lastCalculatedSpeed = AccessTools.Field(typeof(MobileParty), "_lastCalculatedSpeed").GetValue(mobileParty);
        var explanations = mobileParty.SpeedExplained.GetExplanations();

        var stringBuilder = new StringBuilder();
        
        stringBuilder.AppendLine($"MobileParty info for: {owner}");
        stringBuilder.AppendLine($"StringID: {mobileParty.StringId}");
        stringBuilder.AppendLine($"Speed: {mobileParty.Speed}");
        stringBuilder.AppendLine($"DefaultInventoryCapacityModel: {mobileParty.InventoryCapacity}");
        stringBuilder.AppendLine($"Weight Carried: {mobileParty.TotalWeightCarried}");
        stringBuilder.AppendLine($"LastCalculated Speed: {_lastCalculatedSpeed}");
        stringBuilder.AppendLine($"Player Skills: ");
        foreach (SkillObject skill in Skills.All)
        {
            int skillValue = owner.GetSkillValue(skill);
            stringBuilder.AppendLine($"{skill.StringId}: {skillValue}");
        }
        stringBuilder.AppendLine($"Explanations: {explanations}");

        return stringBuilder.ToString();
    }

    // coop.debug.mobileParty.createParty tbd
    [CommandLineArgumentFunction("createParty", "coop.debug.mobileParty")]
    public static string CreateNewParty(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return "Create party is only to be called on the server";
        }

        if (args.Count < 1 || args.Count > 2)
        {
            return "Usage: coop.debug.mobileParty.createParty <CharacterObject.StringId> <optional age>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get {nameof(IObjectManager)}";
        }

        var age = -1;
        if (args.Count == 2 && int.TryParse(args[1], out age) == false)
        {
            return $"{args[1].GetType().Name} was not of type int";
        }

        string characterObjectId = args[0];

        if (objectManager.TryGetObject<CharacterObject>(characterObjectId, out var template) == false)
        {
            return $"Unable to get {typeof(CharacterObject)} with id: {characterObjectId}";
        }

        HeroCreator.CreateBasicHero(template, out var newHero);

        return $"Created new hero with string id: {newHero.StringId}";
    }

    // coop.debug.mobileParty.audit
    [CommandLineArgumentFunction("audit", "coop.debug.mobileParty")]
    public static string AuditHeroes(List<string> args)
    {
        if (ContainerProvider.TryResolve<MobilePartyAuditor>(out var auditor) == false)
        {
            return $"Unable to get {nameof(MobilePartyAuditor)}";
        }

        return auditor.Audit();
    }
}
