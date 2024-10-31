using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scaffolderlord.CLI;
using Scaffolderlord.Helpers;
using System;
using System.Linq;
using System.Reflection;

namespace Scaffolderlord
{
    public static class Extensions
    {
        public static string GetRelativeDirectory(string subPath) => Path.Combine(BannerlordCoopProjectRoot, subPath);
        public static string GetUniqueFilePath(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            string uniqueFilePath = filePath;
            int counter = 1;

            while (File.Exists(uniqueFilePath))
            {
                uniqueFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}({counter++}){extension}");
            }

            return uniqueFilePath;
        }
        public static IServiceCollection PropagateLogger(this IServiceCollection src)
        {
            var logger = src.BuildServiceProvider()
                .GetRequiredService<ILogger<RootCliCommand>>();
            GlobalOptions.GlobalLogger = logger;
            return src;
        }

        public static string PascalCase(this string input, bool removeSpecialChars = false)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            input = removeSpecialChars ? new string(input.Where(char.IsLetterOrDigit).ToArray()) : input;
            return input.Pascalize();
        }

        public static string CamelCase(this string input, bool removeSpecialChars = false)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            input = removeSpecialChars ? new string(input.Where(char.IsLetterOrDigit).ToArray()) : input;
            return input.Camelize();
        }

        private static string getBannerlordCoopProjectRoot()
        {
            var currentExecutionDir = Environment.CurrentDirectory;
            string target = @"BannerlordCoop\source";

            int index = currentExecutionDir.IndexOf(target, StringComparison.OrdinalIgnoreCase);

            if (index >= 0)
            {
                return currentExecutionDir.Substring(0, index + target.Length);
            }

            GlobalOptions.GlobalLogger.LogWarning("Irregular path detected, to ensure files will be created at a correct output path, please call this cli tool from within the package manager console, or the any directory on the Bannerlord Coop project.");
            return currentExecutionDir;
        }
        private static string? _bannerlordCoopProjectRoot;
        public static string BannerlordCoopProjectRoot
        {
            get
            {
                _bannerlordCoopProjectRoot ??= getBannerlordCoopProjectRoot();
                return _bannerlordCoopProjectRoot;
            }
        }

        /// This is a integral part of the code, do not remove
        public static string GetRandomWarbandQuote() => Quotes[new Random().Next(Quotes.Length)];
        private static readonly string[] Quotes = new string[]
        {
        "We'll have our pay. Or we'll have our fun.",
        "I will drink from your skull!",
        "That's a nice head you have on your shoulders!",
        "It’s almost harvesting season!",
        "Less talking, more raiding!",
        "M'lord!",
        "We can do this the easy way, or the hard way.",
        "You better not be a manhunter!",
        "Away with you vile beggar!",
        "Stand and deliver.",
        "Today the gods will decide your fate!",
        "My men would like a word with you about your puuurse-onal belongings.",
        "Out for a stroll, are we?",
        "Blood, honor and glory awaits us!",
        "Hail to you brother of war!",
        "We ride to war!",
        "Hail to the king!",
        "By the sword and by the spear!"
        };
        public static void PrintCommandSucceededMessage()
        {
            string quote = GetRandomWarbandQuote();
            Console.WriteLine("Command completed");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"'{quote}'");
            Console.ResetColor();
        }
    }

}
