using DocumentFormat.OpenXml.Wordprocessing;
using log4net;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.CoreSkills;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using semantic_sandbox.Skills;
using System.Net;

public class Program
{
    public static void Main()
    {
        var kernel = SemanticKernelExample.GetKernel();
        ILogger logger = new LoggerFactory().CreateLogger("RollingLogFileAppender");
        var o = new SemanticKernelExample(kernel, logger);
        o.RunAsync().Wait();
    }
}

