using Scaffolderlord.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Models
{
	//public abstract class ServiceTypeTemplateModel
	//{
	//	protected ServiceTypeInfo ServiceTypeInfo { get; set; }
	//	public ServiceTypeTemplateModel(ServiceTypeInfo serviceTypeInfo)
	//	{
	//		ServiceTypeInfo = serviceTypeInfo;
	//	}
	//}

	public interface ITemplateModel
	{
		string TemplateFileName { get; }
		string? Namespace { get; }
		string[] Usings { get; }

		string GetTemplateFilePath()
		{
			return Path.Combine(GetRelativePath("Templates"), $"{TemplateFileName}.cshtml");
		}
		string GetOutputPath();
	}

}
