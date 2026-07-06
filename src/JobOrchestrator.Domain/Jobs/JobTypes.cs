using System;
using System.ComponentModel;

namespace JobOrchestrator.Domain.Jobs
{
	public enum JobTypes
	{
		[Description("Job de demonstração")]
		Demo,
		
		[Description("Job de demonstração com atraso")]
		DemoDelayed
	}
}
