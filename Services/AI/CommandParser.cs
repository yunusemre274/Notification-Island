using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NI.Services.AI
{
    public enum CommandType
    {
        CreateFile,
        CreateFolder,
        ListFiles,
        MoveFile,
        GetSystemInfo,
        Deny,
        Unknown
    }

    public class ParsedCommand
    {
        public CommandType Type { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
        public string OriginalInput { get; set; } = string.Empty;
    }

    public class CommandParser
    {
        // System prompt for Ollama
        private const string SYSTEM_PROMPT = @"
You are a Windows command interpreter. Parse user requests into structured commands.

Available commands:
1. CREATE_FILE: {""filename"": ""name.ext"", ""location"": ""path""}
2. FIND_FOLDER: {""name"": ""folder_name""}
3. GET_PATH: {""location"": ""Desktop|Downloads|Documents""}
4. LIST_FILES: {""location"": ""path"", ""extension"": "".txt""}

Respond ONLY with JSON. Example:
User: ""Create file text.txt on Desktop""
Response: {""command"": ""CREATE_FILE"", ""filename"": ""text.txt"", ""location"": ""Desktop""}

User: ""Find Projects folder""
Response: {""command"": ""FIND_FOLDER"", ""name"": ""Projects""}
";

        private readonly OllamaClient _ollama;

        public CommandParser(OllamaClient ollama)
        {
            _ollama = ollama;
        }

        public async Task<ParsedCommand> ParseAsync(string userInput,
                                                    CancellationToken ct = default)
        {
            var prompt = $"{SYSTEM_PROMPT}\n\nUser: \"{userInput}\"\nResponse:";

            var response = await _ollama.GenerateAsync(prompt, ct);

            // Parse JSON response
            return ParseJsonResponse(response, userInput);
        }

        private ParsedCommand ParseJsonResponse(string json, string originalInput)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var action = root.GetProperty("action").GetString();
                var type = action switch
                {
                    "create_file" => CommandType.CreateFile,
                    "create_folder" => CommandType.CreateFolder,
                    "list_files" => CommandType.ListFiles,
                    "move_file" => CommandType.MoveFile,
                    "get_system_info" => CommandType.GetSystemInfo,
                    "deny" => CommandType.Deny,
                    _ => CommandType.Unknown
                };

                var parameters = new Dictionary<string, string>();
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Name != "action")
                        parameters[prop.Name] = prop.Value.GetString() ?? "";
                }

                return new ParsedCommand
                {
                    Type = type,
                    Parameters = parameters,
                    OriginalInput = originalInput
                };
            }
            catch
            {
                return new ParsedCommand
                {
                    Type = CommandType.Unknown,
                    OriginalInput = originalInput
                };
            }
        }
    }
}
