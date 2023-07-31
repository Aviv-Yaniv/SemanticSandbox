﻿using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Skills.Core;
using semantic_sandbox.Skills;
using System.Net;

public sealed class SemanticKernelExample
{

    private const string MemoryCollectionName = "Kusto";
    private readonly IKernel _kernel;
    private readonly TextMemorySkill _memorySkill;

    public SemanticKernelExample(IKernel kernel) =>
            (_kernel, _memorySkill) = (kernel, new());

    private async void MemorySanityTest()
    {
        string ask = "Find function";
        Console.WriteLine("===========================\n" +
                            "Query: " + ask + "\n");

        var memories = _kernel.Memory.SearchAsync(MemoryCollectionName, ask, limit: 5, minRelevanceScore: 0.2);

        var i = 0;
        await foreach (MemoryQueryResult memory in memories)
        {
            Console.WriteLine($"Result {++i}:");
            var prompt = @"Answer: {{$input}} \n Referring to: " + memory.Metadata.Text;
            var ans = _kernel.CreateSemanticFunction(prompt, maxTokens: 5 * 1024);
            Console.WriteLine("  answer: " + await ans.InvokeAsync(ask));

            Console.WriteLine();
            Console.WriteLine("  URL:     : " + memory.Metadata.Id);
            Console.WriteLine("  Title    : " + memory.Metadata.Description);
            Console.WriteLine("  Relevance: " + memory.Relevance);
            Console.WriteLine();
        }
    }

    private async void AutoCompleteSanityTest()
    {
        string prompt = "What is the answer to life and the universe?";
        
        var answer = _kernel.CreateNewContext();

        try
        {
            var chatFunction = _kernel.CreateSemanticFunction(prompt, maxTokens: 256, topP: 0.2, temperature: 1);
            answer = chatFunction.InvokeAsync(prompt).Result;
        }
        catch (Exception e)
        {
            await Console.Out.WriteLineAsync(e.ToString());
        }
        
        Console.WriteLine("Answer: " + answer);
    }

    public async Task RunAsync()
    {
        // AutoCompleteSanityTest();
        // return;

        var context = _kernel.CreateNewContext();
        await BuildMemory(context);

        _kernel.ImportSkill(_memorySkill);
        var pc = _kernel.AddConsultantProfile();
        //context = _kernel.CreateNewContext();

        context[TextMemorySkill.CollectionParam] = MemoryCollectionName;
        context[TextMemorySkill.LimitParam] = "3";

        // MemorySanityTest();

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Enter question:");
            Console.ResetColor();

            var query = Console.ReadLine();
            context["query"] = query;

            var response =  await pc.InvokeAsync(query);
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
            await _kernel.Memory.SaveInformationAsync(MemoryCollectionName, section, $"ph-{name}-{index + 1:00}");
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

    public static IKernel GetKernel()
    {
        var builder = new KernelBuilder();

        var CredentialTokens = File.ReadAllText(@"../../../CREDENTIALS.txt").Split(",");
        var (azureEndpoint, apiKey, embeddingDeploymentName, textDeployment) = (CredentialTokens[0], CredentialTokens[1], CredentialTokens[2], CredentialTokens[3]);

        builder.WithAzureTextEmbeddingGenerationService(embeddingDeploymentName, azureEndpoint, apiKey);
        builder.WithAzureTextCompletionService(textDeployment, azureEndpoint, apiKey);

        builder.WithMemoryStorage(new VolatileMemoryStore());

        var kernel = builder.Build();

        return kernel;
    }
}

