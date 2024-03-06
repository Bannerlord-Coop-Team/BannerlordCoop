using Common.Logging;
using Common.Util;
using HarmonyLib;
using Serilog;
using System.Diagnostics;
using System.Reflection;

namespace GameInterface.Services.GameDebug.Patches
{
    /// <summary>
    /// Helper functions for call stack validation in Harmony patches
    /// </summary>
    public class CallStackValidator
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CallStackValidator>();

        /// <summary>
        /// Checks the call stack for calls from the Coop mod, if the function is called and the allowed instance isn't set
        /// the call from the mod is not managed correctly.
        /// 
        /// If there is a call from the mod, all relevant allowed instances must be set.
        /// 
        /// NOTE: this is fairly slow so it will only be enabled during debug builds
        /// </summary>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <param name="instance">Instance to check if allowed</param>
        /// <param name="allowedInstance">Allowed Instance for given patch</param>
        public static void Validate<TInstance>(TInstance instance, AllowedInstance<TInstance> allowedInstance) where TInstance : class
        {
#if DEBUG
            var callstack = new StackTrace(1);

            foreach (var frame in callstack.GetFrames())
            {
                var method = frame.GetMethod();

                // skip if method in mod but is a harmony patch
                if (method?.GetCustomAttribute<HarmonyPrefix>() != null ||
                    method?.GetCustomAttribute<HarmonyPostfix>() != null ||
                    method?.Name == "Prefix" ||
                    method?.Name == "Postfix")
                {
                    continue;
                }

                var methodNamespace = method?.DeclaringType?.Namespace;

                if (methodNamespace == null) continue;

                // If namespace of method is in mod, do instance check
                if (methodNamespace.StartsWith("GameInterface") ||
                    methodNamespace.StartsWith("Coop"))
                {
                    // If instance is not allowed, we have called the method from the mod but are not calling it properly
                    if (instance != allowedInstance.Instance)
                    {
                        // Gets the method name of the calling patch,
                        // currently 1 stack call above this method (at least should be)
                        var patchName = callstack.GetFrame(1)?.GetMethod()?.Name;
                        // Gets the method name of the mod method calling the patch
                        var modMethodName = method.Name;

                        Logger.Warning("{patchName}() has been called incorrectly from {modMethodName}(). " +
                            "This means {modMethodName}() requires AllowedInstance to be set before " +
                            "calling or another method of managing the call (i.e. transpiler) ", patchName, modMethodName);
                    }
                }
            }
#endif
        }
    }
}
