using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library.NewsManager;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Patches
{
    [HarmonyPatch]
    internal class HeroFieldPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroFieldPatches>();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(var method in AccessTools.GetDeclaredMethods(typeof(Hero)))
            {
                yield return method;
            }
            yield return AccessTools.Method(typeof(HeroDeveloper), "CheckLevel");
            yield return AccessTools.Method(typeof(HeroDeveloper), "ClearHeroLevel");
            yield return AccessTools.Method(typeof(MakePregnantAction), nameof(MakePregnantAction.ApplyInternal));
            yield return AccessTools.Method(typeof(PregnancyCampaignBehavior), "CheckOffspringsToDeliver", new Type[] { typeof(Hero) });
            yield return AccessTools.Method(typeof(PregnancyCampaignBehavior), "CheckOffspringsToDeliver", new Type[] { typeof(PregnancyCampaignBehavior.Pregnancy) });
            yield return AccessTools.Method(typeof(HeroCreator), nameof(HeroCreator.CreateRelativeNotableHero));
            yield return AccessTools.Method(typeof(HeroCreator), nameof(HeroCreator.DeliverOffSpring));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> LastTimeStampForActivityTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var lastTimeStampField = AccessTools.Field(typeof(Hero), nameof(Hero.LastTimeStampForActivity));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(LastTimeStampForActivityIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(lastTimeStampField))
                {
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void LastTimeStampForActivityIntercept(Hero instance, int newTimestamp)
        {
            // Change instance for all branches (prevent crashing)
            instance.LastTimeStampForActivity = newTimestamp;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new LastTimeStampChanged(newTimestamp, instance.StringId));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CharacterObjectTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var characterObjectField = AccessTools.Field(typeof(Hero), nameof(Hero._characterObject));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(CharacterObjectIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(characterObjectField))
                {
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void CharacterObjectIntercept(Hero instance, CharacterObject newCharacterObject)
        {
            // Change instance for all branches (prevent crashing)
            instance._characterObject = newCharacterObject;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new CharacterObjectChanged(newCharacterObject.StringId, instance.StringId));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> FirstNameTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var firstNameField = AccessTools.Field(typeof(Hero), nameof(Hero._firstName));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(FirstNameIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(firstNameField))
                {
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void FirstNameIntercept(Hero instance, TextObject newName)
        {
            // Change instance for all branches (prevent crashing)
            instance._firstName = newName;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new FirstNameChanged(newName.Value, instance.StringId));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> NameTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var nameField = AccessTools.Field(typeof(Hero), nameof(Hero._name));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(NameIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(nameField))
                {
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void NameIntercept(Hero instance, TextObject newName)
        {
            // Change instance for all branches (prevent crashing)
            instance._name = newName;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new NameChanged(newName.Value, instance.StringId));
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> HairTagsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var hairTagsField = AccessTools.Field(typeof(Hero), nameof(Hero.HairTags));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(HairTagsIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(hairTagsField))
                {
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void HairTagsIntercept(Hero instance, string newTags)
        {
            // Change instance for all branches (prevent crashing)
            instance.HairTags = newTags;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new HairTagsChanged(newTags, instance.StringId));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> BeardTagsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var beardTagsField = AccessTools.Field(typeof(Hero), nameof(Hero.BeardTags));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(BeardTagsIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(beardTagsField))
                {
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void BeardTagsIntercept(Hero instance, string newTags)
        {
            // Change instance for all branches (prevent crashing)
            instance.BeardTags = newTags;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new BeardTagsChanged(newTags, instance.StringId));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TattooTagsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var tattooTagsField = AccessTools.Field(typeof(Hero), nameof(Hero.TattooTags));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(TattooTagsIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(tattooTagsField))
                {
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void TattooTagsIntercept(Hero instance, string newTags)
        {
            // Change instance for all branches (prevent crashing)
            instance.TattooTags = newTags;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new TattooTagsChanged(newTags, instance.StringId));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> HeroStateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var heroStateField = AccessTools.Field(typeof(Hero), nameof(Hero._heroState));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(HeroStateIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(heroStateField))
                {
                    CodeInstruction codeInst = new CodeInstruction(OpCodes.Call, fieldIntercept);
                    codeInst.labels = instruction.labels;
                    yield return codeInst;
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void HeroStateIntercept(Hero instance, Hero.CharacterStates newState)
        {
            // Change instance for all branches (prevent crashing)
            instance._heroState = newState;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new HeroStateChanged((int)newState, instance.StringId));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SpcDaysInLocationTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var spcDaysInLocationField = AccessTools.Field(typeof(Hero), nameof(Hero.SpcDaysInLocation));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(SpcDaysInLocationIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(spcDaysInLocationField))
                {
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void SpcDaysInLocationIntercept(Hero instance, int days)
        {
            // Change instance for all branches (prevent crashing)
            instance.SpcDaysInLocation = days;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new SpcDaysInLocationChanged(days, instance.StringId));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> DefaultAgeTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var valueField = AccessTools.Field(typeof(Hero), nameof(Hero._defaultAge));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(DefaultAgeIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(valueField))
                {
                    CodeInstruction codeInst = new CodeInstruction(OpCodes.Call, fieldIntercept);
                    codeInst.labels = instruction.labels;
                    yield return codeInst;
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void DefaultAgeIntercept(Hero instance, float age)
        {
            // Change instance for all branches (prevent crashing)
            instance._defaultAge = age;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new DefaultAgeChanged(age, instance.StringId));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> BirthDayTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var valueField = AccessTools.Field(typeof(Hero), nameof(Hero._birthDay));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(BirthDayIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(valueField))
                {
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void BirthDayIntercept(Hero instance, CampaignTime birthDay)
        {
            // Change instance for all branches (prevent crashing)
            instance._birthDay = birthDay;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new BirthDayChanged(birthDay.NumTicks, instance.StringId)); 
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PowerTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var valueField = AccessTools.Field(typeof(Hero), nameof(Hero._power));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(PowerIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(valueField))
                {
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void PowerIntercept(Hero instance, float power)
        {
            // Change instance for all branches (prevent crashing)
            instance._power = power;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new PowerChanged(power, instance.StringId));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> HomeSettlementTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var valueField = AccessTools.Field(typeof(Hero), nameof(Hero._homeSettlement));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(HomeSettlementIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(valueField))
                {
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void HomeSettlementIntercept(Hero instance, Settlement settlement)
        {
            // Change instance for all branches (prevent crashing)
            instance._homeSettlement = settlement;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new HomeSettlementChanged(settlement?.StringId, instance.StringId));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PregnantTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var valueField = AccessTools.Field(typeof(Hero), nameof(Hero.IsPregnant));
            var fieldIntercept = AccessTools.Method(typeof(HeroFieldPatches), nameof(PregnantIntercept));

            foreach (var instruction in instructions)
            {
                if (instruction.StoresField(valueField))
                {
                    yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static void PregnantIntercept(Hero instance, bool isPregnant)
        {
            // Change instance for all branches (prevent crashing)
            instance.IsPregnant = isPregnant;

            if (CallPolicy.IsOriginalAllowed()) return;
            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return;

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(instance, new PregnantChanged(isPregnant, instance.StringId));
        }
    }
}