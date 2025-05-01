using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
namespace GameInterface.Services.Kingdoms.Patches;


/// <summary>
/// Patches for managing lifetime of <see cref="Kingdom"/> objects.
/// </summary>
[HarmonyPatch(typeof(Kingdom))]
internal class KingdomLifetimePatches
{
	private static readonly ILogger Logger = LogManager.GetLogger<KingdomLifetimePatches>();

	[HarmonyPatch(typeof(Kingdom))]
	[HarmonyPatch(MethodType.Constructor)]
	[HarmonyPrefix]
	private static bool ConstructorPrefix(ref Kingdom __instance)
	{
		// Call original if we call this function
		if (CallPolicy.IsOriginalAllowed()) return true;

		if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

		var message = new KingdomCreated(__instance);

		ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
		messageBroker?.Publish(__instance, message);

		return true;
	}
}
