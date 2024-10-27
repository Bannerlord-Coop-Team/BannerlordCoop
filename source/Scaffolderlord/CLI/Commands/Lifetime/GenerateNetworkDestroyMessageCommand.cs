using DotMake.CommandLine;
using Scaffolderlord.Helpers;
using Scaffolderlord.Models;
using Scaffolderlord.Models.Lifetime;
using Scaffolderlord.Services;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord.CLI.Commands
{
	[CliCommand(
		Name = "lifetime-messages-network-destroy",
		Description = "Generates lifetime network destroy message",
		Parent = typeof(RootCliCommand)
		)]
	public class GenerateNetworkDestroyMessageCommand : GenerateCommandBase<NetworkDestroyMessageTemplateModel>
	{
		public GenerateNetworkDestroyMessageCommand(IScaffoldingService scaffoldingService) : base(scaffoldingService)
		{
		}
	}
}
