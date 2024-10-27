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
		Name = "lifetime-messages-network-create",
		Description = "Generates lifetime network create message",
		Parent = typeof(RootCliCommand)
		)]
	public class GenerateNetworkCreateMessageCommand : GenerateCommandBase<NetworkCreateMessageTemplateModel>
	{
		public GenerateNetworkCreateMessageCommand(IScaffoldingService scaffoldingService) : base(scaffoldingService)
		{
		}
	}
}
