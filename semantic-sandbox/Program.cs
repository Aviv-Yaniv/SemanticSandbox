using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Skills.Core;
using Microsoft.SemanticKernel.Skills.Document.FileSystem;
using Microsoft.SemanticKernel.Skills.Document.OpenXml;
using Microsoft.SemanticKernel.Skills.Document;
using semantic_sandbox.Skills;
using DocumentFormat.OpenXml.Office.PowerPoint.Y2021.M06.Main;
using System.Net;

public sealed class ProjectHistoryExample
{
    
    private const string MemoryCollectionName = "ProjectHistory";
    private readonly IKernel _kernel;
    private readonly TextMemorySkill _memorySkill;

    public ProjectHistoryExample(IKernel kernel) =>
        (_kernel, _memorySkill) = (kernel, new());

    public async Task RunAsync()
    {
        var context = _kernel.CreateNewContext();
        await BuildMemory(context);

        _kernel.ImportSkill(_memorySkill);
        var pc = _kernel.AddConsultantProfile();
        //context = _kernel.CreateNewContext();

        context[TextMemorySkill.CollectionParam] = MemoryCollectionName;
        context[TextMemorySkill.LimitParam] = "3";

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Enter question:");
            Console.ResetColor();

            var query = Console.ReadLine();
            context["query"] = query;
            var response = await pc.InvokeAsync(context);

            WriteResponse(response);
        }
    }

    static string ReadFile(string url)
    {
        WebClient client = new WebClient();
        Stream stream = client.OpenRead(url);
        StreamReader reader = new StreamReader(stream);
        String content = reader.ReadToEnd();
        return content;
    }

    private IEnumerable<(string, string)> GetMemoriesData()
    {
        var githubFiles = new Dictionary<string, string>()
        {
            // ["https://github.com/microsoft/semantic-kernel/blob/main/README.md"]
            //     = "README: Installation, getting started, and how to contribute",
            // /*
            // ["https://github.com/microsoft/semantic-kernel/blob/main/samples/notebooks/dotnet/02-running-prompts-from-file.ipynb"]
            //     = "Jupyter notebook describing how to pass prompts from a file to a semantic skill or function",
            // */
            // ["https://github.com/microsoft/semantic-kernel/blob/main/samples/notebooks/dotnet/00-getting-started.ipynb"]
            //     = "Jupyter notebook describing how to get started with the Semantic Kernel",
            // /*
            // ["https://github.com/microsoft/semantic-kernel/blob/main/dotnet/src/SemanticKernel/Memory/Volatile/VolatileMemoryStore.cs"]
            //     = "C# class that defines a volatile embedding store",
            // */
            // ["https://github.com/microsoft/semantic-kernel/tree/main/samples/dotnet/KernelHttpServer/README.md"]
            //     = "README: How to set up a Semantic Kernel Service API using Azure Function Runtime v4",
            //["https://raw.githubusercontent.com/microsoft/Kusto-Query-Language/master/doc/clusterfunction.md"]
            //= "Cluster function",
            ["https://raw.githubusercontent.com/microsoft/Kusto-Query-Language/master/doc/anomaly-detection.md"]
            = "Anomaly detection function",
        };

        foreach (var entry in githubFiles)
        {
            var url = entry.Key;
            var name = entry.Value;
            Console.WriteLine($"Memorizing {url}");
            var file = ReadFile(url);
            yield return (file, name);
        }
    }

    private async Task BuildMemory(SKContext context)
    {
        foreach ((string text, string name) in GetMemoriesData())
        {
            await AddSingleMemory(text, name);
        }
    }

    private async Task AddSingleMemory(string text, string name)
    {
        var sections = text
                    .Split($"{Environment.NewLine}{Environment.NewLine}", StringSplitOptions.RemoveEmptyEntries)
                    .Where(s => s.Split(" ").Length > 2)
                    .Select(e => e.Trim())
                    .ToArray();

        for (var i = 0; i < sections.Length; i++)
        {
            var index = i + 1;
            var section = sections[i];
            var wordCount = section.Split(' ').Length;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Storing: {index}/{sections.Length}");
            Console.ResetColor();
            Console.WriteLine(section);
            await _kernel.Memory.SaveInformationAsync(name, section, $"ph-{name}-{index + 1:00}");
        }

        Console.WriteLine($"Completed adding new memory!");
    }

    private static void WriteResponse(object response)
    {
        Console.WriteLine();
        Console.WriteLine("GPT:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(response);
        Console.WriteLine();
        Console.ResetColor();
    }

    private static IKernel GetKernel()
    {
        var builder = new KernelBuilder();

        var CredentialTokens = File.ReadAllText(@"../../../CREDENTIALS.txt").Split(",");
        var (azureEndpoint, apiKey, embeddingDeploymentName, textDeployment) = (CredentialTokens[0], CredentialTokens[1], CredentialTokens[2], CredentialTokens[3]);

        builder.WithAzureTextEmbeddingGenerationService(
                 embeddingDeploymentName,   // Azure OpenAI Deployment Name
                 azureEndpoint,             // Azure OpenAI Endpoint
                 apiKey);                   // Azure OpenAI Key
        builder.WithAzureTextCompletionService(textDeployment, azureEndpoint, apiKey);

        builder.WithMemoryStorage(new VolatileMemoryStore());

        var kernel = builder.Build();

        return kernel;
    }

    public static void Main()
    {
        var kernel = GetKernel();
        var o = new ProjectHistoryExample(kernel);
        o.RunAsync().Wait();
    }
}

