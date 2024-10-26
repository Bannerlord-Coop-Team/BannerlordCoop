using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Scaffolderlord.CLI.Commands;
using Scaffolderlord.Models;

namespace Scaffolderlord
{
	class Program
	{
		static async Task<int> Main(string[] args)
		{
			var rootCommand = new RootCommand("Scaffolding CLI tool for generating files for the BannerlordCoop project");

			InitializeCommands(rootCommand);

			return await rootCommand.InvokeAsync(args);
		}

		private static void InitializeCommands(RootCommand rootCommand)
		{
			GenerateRegistryCommand.InitializeCommand(rootCommand);
			GenerateAutoSyncCommand.InitializeCommand(rootCommand);
		}


	}
}
