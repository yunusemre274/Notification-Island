using System;
using System.Threading;
using System.Threading.Tasks;
using NI.Services.AI;

namespace NI.Services
{
    public class AiAssistantService : IDisposable
    {
        private readonly OllamaClient _ollama;
        private readonly CommandParser _parser;
        private readonly CommandExecutor _executor;
        private bool _isAvailable;

        public bool IsAvailable => _isAvailable;

        public event EventHandler<CommandResultEventArgs>? CommandCompleted;

        public AiAssistantService()
        {
            _ollama = new OllamaClient();
            _parser = new CommandParser(_ollama);
            _executor = new CommandExecutor();
        }

        public async Task InitializeAsync()
        {
            _isAvailable = await _ollama.IsAvailableAsync();
        }

        public async Task<CommandExecutionResult> ProcessCommandAsync(
            string userInput,
            CancellationToken ct = default)
        {
            if (!_isAvailable)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Error = "Ollama is not available. Please start Ollama."
                };
            }

            // Step 1: Parse command using AI
            var parsedCommand = await _parser.ParseAsync(userInput, ct);

            // Step 2: Execute validated command
            var result = await _executor.ExecuteAsync(parsedCommand);

            // Step 3: Fire event
            CommandCompleted?.Invoke(this, new CommandResultEventArgs
            {
                Input = userInput,
                Result = result
            });

            return result;
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }

    public class CommandResultEventArgs : EventArgs
    {
        public string Input { get; init; } = string.Empty;
        public CommandExecutionResult Result { get; init; } = null!;
    }
}
