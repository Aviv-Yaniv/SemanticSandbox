using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.SkillDefinition;

namespace semantic_sandbox.Skills
{
    public static class ConsultantProfileSkill
    {
        private const string PromptTemplate = @"
I am a consultant who answers questions according to my experience.

I ensure my responses contain only the most relevant information to questions I am asked.

Base on 'relevant information' when writing a response to answer a question.

relevant information: {{recall $query}}

Question: {{$query}}

Answer:
";
        internal static ISKFunction AddConsultantProfile(this IKernel kernel) =>
            kernel.CreateSemanticFunction(promptTemplate: PromptTemplate, maxTokens: 256);
    }
}
