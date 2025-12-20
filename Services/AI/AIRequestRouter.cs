using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NI.Services.AI
{
    public enum IntentType
    {
        Agent,
        Chat
    }

    public class RouteResult
    {
        public IntentType Intent { get; set; }
        public string UserInput { get; set; } = string.Empty;
    }

    public class AIRequestRouter
    {
        private static readonly string[] AgentKeywords = new[]
        {
            "create",
            "delete",
            "move",
            "list",
            "open",
            "folder",
            "file",
            "desktop",
            "system",
            "cpu",
            "ram",
            "disk",
            "ssd"
        };

        private readonly OllamaClient _ollamaClient;

        public AIRequestRouter(OllamaClient ollamaClient)
        {
            _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        }

        public RouteResult DetermineIntent(string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return new RouteResult
                {
                    Intent = IntentType.Chat,
                    UserInput = userInput
                };
            }

            var inputLower = userInput.ToLowerInvariant();
            var isAgent = AgentKeywords.Any(keyword => inputLower.Contains(keyword));

            return new RouteResult
            {
                Intent = isAgent ? IntentType.Agent : IntentType.Chat,
                UserInput = userInput
            };
        }

        public async Task<string> ProcessAgentRequestAsync(string userInput, CancellationToken cancellationToken = default)
        {
            return await _ollamaClient.GenerateAgentAsync(userInput, cancellationToken);
        }

        public async Task<string> ProcessChatRequestAsync(string userInput, CancellationToken cancellationToken = default)
        {
            return await _ollamaClient.GenerateChatAsync(userInput, cancellationToken);
        }
    }
}
