using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ModTestingFramework.RunnerCommunicator;

namespace ModTestingFramework
{
    class RegistrationFailedException : Exception
    {
        public RegistrationFailedException(string message) : base(message)
        {
        }
    }
    class CommandRegistry
    {
        public static Dictionary<string, MethodInfo> commands = new Dictionary<string, MethodInfo>();
        private static CommandRegistry instance;

        public static void RegisterAllCommands()
        {
            List<Task> tasks = new List<Task>();

            foreach (Type type in AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => typeof(TestCommands).IsAssignableFrom(p)))
            {
                foreach (MethodInfo methodInfo in type.GetMethods().Where(m => m.DeclaringType != typeof(object)))
                {
                    if (commands.ContainsKey(methodInfo.Name))
                    {
                        throw new RegistrationFailedException($"{methodInfo.Name} has already been registered.");
                    }
                    else if (!methodInfo.IsStatic)
                    {
                        throw new RegistrationFailedException($"{methodInfo.Name} must be static.");
                    }
                    else if (methodInfo.GetParameters().Length >= 2)
                    {
                        throw new RegistrationFailedException($"{methodInfo.Name} must only have up to one parameter.");
                    }
                    else if (methodInfo.GetParameters().Length == 1 &&
                        methodInfo.GetParameters()[0].ParameterType != typeof(string[]))
                    {
                        throw new RegistrationFailedException($"{methodInfo.Name} must have a string[] parameter.");
                    }
                    else if (methodInfo.ReturnType != typeof(string) &&
                        methodInfo.ReturnType != typeof(void))
                    {
                        throw new RegistrationFailedException($"{methodInfo.Name} must only return void or string.");
                    }
                    else
                    {
                        tasks.Add(RegisterCommand(methodInfo.Name, methodInfo));
                    }
                }
            }
            Task.WhenAll(tasks).ContinueWith((t) => SendRegistrationComplete());
        }


        private readonly static RunnerCommunicator communicator = RunnerCommunicator.Instance;

        private static void SendRegistrationComplete()
        {
            communicator.SendData("REGISTRATION_COMPLETE");
        }

        private static Task RegisterCommand(string command, MethodInfo methodInfo)
        {
            return Task.Run(() => AsyncRegisterCommand(command, methodInfo));
        }

        private static async Task AsyncRegisterCommand(string command, MethodInfo methodInfo)
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>(false);

            // Create ACK Handler
            MessageReceivedDelegate ACK_Handler = (sender, msg) =>
            {
                if (msg.MessageString == $"REGISTERED {command}")
                {
                    _tcs.SetResult(true);
                }
                else if (msg.MessageString == $"REGISTER_FAILED {command}")
                {
                    _tcs.SetResult(false);
                }
            };

            cts.Token.Register(() => {
                if (!_tcs.Task.IsCompleted)
                {
                    _tcs.SetResult(false);
                }
            });

            // Register Handler
            communicator.OnMessageReceived += ACK_Handler;

            // Register command
            communicator.SendData($"REGISTER {command}");

            cts.CancelAfter(RunnerCommunicator.Instance.ACK_TIMEOUT);

            if (await _tcs.Task)
            {
                commands.Add(command, methodInfo);
            }
            else
            {
                // Unregister Handler
                RunnerCommunicator.Instance.OnMessageReceived -= ACK_Handler;
                throw new RegistrationFailedException($"Registration failed for {command}");
            }

            // Unregister Handler
            RunnerCommunicator.Instance.OnMessageReceived -= ACK_Handler;
        }

        public static CommandRegistry Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new CommandRegistry();
                }

                return instance;
            }
        }
    }
}
