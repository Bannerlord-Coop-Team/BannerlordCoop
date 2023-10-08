using Common.Logging;
using Common.Util;
using HarmonyLib;
using Serilog;
using System.Diagnostics;
using System.Reflection;

namespace GameInterface.Services.GameDebug.Patches
{
    internal class CallStackValidator
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CallStackValidator>();

        public static void Validate<TInstance>(TInstance instance, AllowedInstance<TInstance> allowedInstance) where TInstance : class
        {
#if DEBUG
            var callstack = new StackTrace(1);

            foreach (var frame in callstack.GetFrames())
            {
                var method = frame.GetMethod();

                if (method?.GetCustomAttribute<HarmonyPrefix>() != null ||
                    method?.GetCustomAttribute<HarmonyPostfix>() != null ||
                    method?.Name == "Prefix" ||
                    method?.Name == "Postfix")
                {
                    continue;
                }

                var methodNamespace = method?.DeclaringType?.Namespace;

                if (methodNamespace == null) continue;

                if (methodNamespace.StartsWith("GameInterface") ||
                    methodNamespace.StartsWith("Coop"))
                {
                    // If instance is not allowed, we have called the method from the mod but are not calling it properly
                    if (instance != allowedInstance.Instance)
                    {
                        var methodName = callstack.GetFrame(1)?.GetMethod()?.Name;
                        var modMethodName = method.Name;

                        Logger.Warning("{methodName} has been called incorrectly from {modMethodName}. " +
                            "This means {modMethodName} requires AllowedInstance to be set before " +
                            "calling or another method of managing the call (i.e. transpiler) ", methodName, modMethodName);
                    }
                }
            }
#endif
        }
    }
}
