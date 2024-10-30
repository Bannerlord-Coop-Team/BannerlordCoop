using Scaffolderlord.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models.E2E
{
	public class E2EFieldTestsTemplateModel : BaseTemplateModel, ITemplateModel
	{
		public string TypeName { get; }
		public string? Namespace { get; }
		public IEnumerable<string> Usings { get; }
		public IEnumerable<FieldInfo> Fields { get; }

		public string TemplateFileName => @"E2E\E2EFieldTestsTemplate.cshtml";

		public string GetOutputPath() => GetMainProjectPath(@$"E2E.Tests\Services\{TypeName}s\{TypeName}FieldTests.cs");

		public IEnumerable<FieldInfo> GetStructFields() => Fields.Where(x => x.FieldType.IsStruct());
		public IEnumerable<FieldInfo> GetClassFields() => Fields.Where(x => !x.FieldType.IsStruct());

		public E2EFieldTestsTemplateModel(ServiceTypeInfo serviceInfo)
		{
			TypeName = serviceInfo.Type.Name;
			Namespace = $"E2E.Tests.Services.{serviceInfo.Type.Name}s;";
			Fields = serviceInfo.Fields;
			Usings = new List<string>
			{
				serviceInfo.Type.Namespace!
			};

			var requiredNamespaces = Fields
				.Select(x => x.FieldType.Namespace)
				.Where(x => x != null)
				.Distinct();


			if (requiredNamespaces.Any()) (Usings as List<string>)!.AddRange(requiredNamespaces!);
		}
	}
}
