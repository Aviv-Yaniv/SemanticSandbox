using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.Memory;
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;

const string memoryCollectionName = "SKGitHub";

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
    ["https://raw.githubusercontent.com/microsoft/Kusto-Query-Language/master/doc/clusterfunction.md"]
        = "Cluster function",
};

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

static string ReadFile(string url)
{
    WebClient client = new WebClient();
    Stream stream = client.OpenRead(url);
    StreamReader reader = new StreamReader(stream);
    String content = reader.ReadToEnd();
    return content;
}
static IEnumerable<string> Segmentize(string str, int maxChunkSize)
{
    for (int i = 0; i < str.Length; i += maxChunkSize)
    {
        yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
    }
}

Console.WriteLine("Adding some GitHub file URLs and their descriptions to a volatile Semantic Memory.");
var i = 0;
foreach (var entry in githubFiles)
{
    var file = ReadFile(url: entry.Key);

    var segments = Segmentize(file, 5 * 1024);

    foreach (var segment in segments)
    {
        await kernel.Memory.SaveReferenceAsync(
        collection: memoryCollectionName,
        description: entry.Value,
        text: segment, //entry.Value,
        externalId: entry.Key,
        externalSourceName: "GitHub").ConfigureAwait(true);
        break;
    }

    Console.WriteLine($"  URL {++i} saved");
}

string ask = "How to use cluster function";
Console.WriteLine("===========================\n" +
                    "Query: " + ask + "\n");

var memories = kernel.Memory.SearchAsync(memoryCollectionName, ask, limit: 5, minRelevanceScore: 0.2);

i = 0;
await foreach (MemoryQueryResult memory in memories)
{
    Console.WriteLine($"Result {++i}:");
    var prompt = @"Answer: {{$input}} \n Referring to: " + ReadFile(url: memory.Metadata.Id);
    var ans = kernel.CreateSemanticFunction(prompt, maxTokens: 5 * 1024);
    Console.WriteLine("  answer: " + await ans.InvokeAsync(ask).ConfigureAwait(true));

    Console.WriteLine();
    Console.WriteLine("  URL:     : " + memory.Metadata.Id);
    Console.WriteLine("  Title    : " + memory.Metadata.Description);
    Console.WriteLine("  Relevance: " + memory.Relevance);
    Console.WriteLine();    
}
