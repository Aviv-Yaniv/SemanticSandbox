using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;

public class Program
{
    public static void Main()
    {
        // Sanity.ChatSanityAsync().Wait();
        // return;

        var kernel = SemanticKernelExample.GetKernel();
        var o = new SemanticKernelExample(kernel);
        o.RunAsync().Wait();
    }
}

