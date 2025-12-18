using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NI.Services.AI
{
    public class CommandExecutionResult
    {
        public bool Success { get; set; }
        public string? Result { get; set; }
        public string? Error { get; set; }
    }

    public class CommandExecutor
    {
        private readonly string _userProfile;

        public CommandExecutor()
        {
            _userProfile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
        }

        public async Task<CommandExecutionResult> ExecuteAsync(ParsedCommand command)
        {
            try
            {
                return command.Type switch
                {
                    CommandType.CreateFile => await CreateFileAsync(command.Parameters),
                    CommandType.FindFolder => await FindFolderAsync(command.Parameters),
                    CommandType.GetFolderPath => GetFolderPath(command.Parameters),
                    CommandType.ListFiles => await ListFilesAsync(command.Parameters),
                    _ => new CommandExecutionResult
                    {
                        Success = false,
                        Error = "Unknown command"
                    }
                };
            }
            catch (Exception ex)
            {
                return new CommandExecutionResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        private async Task<CommandExecutionResult> CreateFileAsync(
            Dictionary<string, string> parameters)
        {
            var filename = parameters.GetValueOrDefault("filename", "file.txt");
            var location = parameters.GetValueOrDefault("location", "Desktop");

            var basePath = GetSpecialFolderPath(location);
            var fullPath = Path.Combine(basePath, filename);

            // Validate path (security)
            if (!fullPath.StartsWith(_userProfile))
                return new CommandExecutionResult
                {
                    Success = false,
                    Error = "Access denied: Path outside user directory"
                };

            await File.WriteAllTextAsync(fullPath, string.Empty);

            return new CommandExecutionResult
            {
                Success = true,
                Result = $"Created: {fullPath}"
            };
        }

        private async Task<CommandExecutionResult> FindFolderAsync(
            Dictionary<string, string> parameters)
        {
            var name = parameters.GetValueOrDefault("name", "");

            // Search in common locations
            var searchPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(_userProfile, "Downloads"),
                _userProfile
            };

            foreach (var basePath in searchPaths)
            {
                var dirs = await Task.Run(() => Directory.GetDirectories(basePath, name,
                    SearchOption.TopDirectoryOnly));

                if (dirs.Length > 0)
                {
                    return new CommandExecutionResult
                    {
                        Success = true,
                        Result = dirs[0]
                    };
                }
            }

            return new CommandExecutionResult
            {
                Success = false,
                Error = $"Folder '{name}' not found"
            };
        }

        private CommandExecutionResult GetFolderPath(
            Dictionary<string, string> parameters)
        {
            var location = parameters.GetValueOrDefault("location", "Desktop");
            var path = GetSpecialFolderPath(location);

            return new CommandExecutionResult
            {
                Success = true,
                Result = path
            };
        }

        private async Task<CommandExecutionResult> ListFilesAsync(
            Dictionary<string, string> parameters)
        {
            var location = parameters.GetValueOrDefault("location", "Desktop");
            var extension = parameters.GetValueOrDefault("extension", "*");

            var basePath = GetSpecialFolderPath(location);
            var pattern = extension == "*" ? "*.*" : $"*{extension}";

            var files = await Task.Run(() => Directory.GetFiles(basePath, pattern));
            var fileList = string.Join("\n", files.Select(Path.GetFileName));

            return new CommandExecutionResult
            {
                Success = true,
                Result = fileList
            };
        }

        private string GetSpecialFolderPath(string location)
        {
            return location.ToLower() switch
            {
                "desktop" => Environment.GetFolderPath(
                    Environment.SpecialFolder.Desktop),
                "downloads" => Path.Combine(_userProfile, "Downloads"),
                "documents" => Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "pictures" => Environment.GetFolderPath(
                    Environment.SpecialFolder.MyPictures),
                _ => Environment.GetFolderPath(
                    Environment.SpecialFolder.Desktop)
            };
        }
    }
}
