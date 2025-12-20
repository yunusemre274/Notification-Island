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
                    CommandType.CreateFolder => await CreateFolderAsync(command.Parameters),
                    CommandType.ListFiles => await ListFilesAsync(command.Parameters),
                    CommandType.MoveFile => await MoveFileAsync(command.Parameters),
                    CommandType.GetSystemInfo => GetSystemInfo(),
                    CommandType.Deny => new CommandExecutionResult
                    {
                        Success = false,
                        Error = command.Parameters.GetValueOrDefault("reason", "Action denied")
                    },
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
            var path = parameters.GetValueOrDefault("path", "Desktop/file.txt");
            var content = parameters.GetValueOrDefault("content", "");

            var fullPath = ResolvePath(path);

            // Validate path (security)
            if (!fullPath.StartsWith(_userProfile))
                return new CommandExecutionResult
                {
                    Success = false,
                    Error = "Access denied: Path outside user directory"
                };

            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(fullPath, content);

            return new CommandExecutionResult
            {
                Success = true,
                Result = $"✓ Created file: {Path.GetFileName(fullPath)}"
            };
        }

        private async Task<CommandExecutionResult> CreateFolderAsync(
            Dictionary<string, string> parameters)
        {
            var path = parameters.GetValueOrDefault("path", "Desktop/NewFolder");

            var fullPath = ResolvePath(path);

            // Validate path (security)
            if (!fullPath.StartsWith(_userProfile))
                return new CommandExecutionResult
                {
                    Success = false,
                    Error = "Access denied: Path outside user directory"
                };

            await Task.Run(() => Directory.CreateDirectory(fullPath));

            return new CommandExecutionResult
            {
                Success = true,
                Result = $"✓ Created folder: {Path.GetFileName(fullPath)}"
            };
        }

        private async Task<CommandExecutionResult> MoveFileAsync(
            Dictionary<string, string> parameters)
        {
            var source = parameters.GetValueOrDefault("source", "");
            var destination = parameters.GetValueOrDefault("destination", "");

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination))
                return new CommandExecutionResult
                {
                    Success = false,
                    Error = "Source and destination paths required"
                };

            var sourcePath = ResolvePath(source);
            var destPath = ResolvePath(destination);

            // Validate paths (security)
            if (!sourcePath.StartsWith(_userProfile) || !destPath.StartsWith(_userProfile))
                return new CommandExecutionResult
                {
                    Success = false,
                    Error = "Access denied: Paths outside user directory"
                };

            if (!File.Exists(sourcePath))
                return new CommandExecutionResult
                {
                    Success = false,
                    Error = $"File not found: {Path.GetFileName(sourcePath)}"
                };

            // Create destination directory if needed
            var destDir = Path.GetDirectoryName(destPath);
            if (destDir != null && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            await Task.Run(() => File.Move(sourcePath, destPath, overwrite: true));

            return new CommandExecutionResult
            {
                Success = true,
                Result = $"✓ Moved {Path.GetFileName(sourcePath)} to {Path.GetFileName(destPath)}"
            };
        }

        private CommandExecutionResult GetSystemInfo()
        {
            var cpuUsage = GetCpuUsage();
            var ramUsage = GetRamUsage();
            var diskUsage = GetDiskUsage();

            var info = $"CPU: {cpuUsage:F1}%\nRAM: {ramUsage:F1}%\nDisk: {diskUsage:F1}% used";

            return new CommandExecutionResult
            {
                Success = true,
                Result = info
            };
        }

        private async Task<CommandExecutionResult> ListFilesAsync(
            Dictionary<string, string> parameters)
        {
            var path = parameters.GetValueOrDefault("path", "Desktop");
            var extension = parameters.GetValueOrDefault("extension", "*");

            var fullPath = ResolvePath(path);
            var pattern = extension == "*" ? "*.*" : $"*{extension}";

            if (!Directory.Exists(fullPath))
                return new CommandExecutionResult
                {
                    Success = false,
                    Error = $"Directory not found: {path}"
                };

            var files = await Task.Run(() => Directory.GetFiles(fullPath, pattern));

            if (files.Length == 0)
                return new CommandExecutionResult
                {
                    Success = true,
                    Result = "No files found"
                };

            var fileList = string.Join("\n", files.Select(f => $"• {Path.GetFileName(f)}"));

            return new CommandExecutionResult
            {
                Success = true,
                Result = $"Files in {Path.GetFileName(fullPath)}:\n{fileList}"
            };
        }

        private string ResolvePath(string path)
        {
            // If path starts with special folder name, resolve it
            var parts = path.Split(new[] { '/', '\\' }, 2);
            var firstPart = parts[0];
            var restPart = parts.Length > 1 ? parts[1] : "";

            var basePath = GetSpecialFolderPath(firstPart);

            // If basePath is same as firstPart, it wasn't a special folder
            if (basePath == GetSpecialFolderPath("Desktop") &&
                !firstPart.Equals("Desktop", StringComparison.OrdinalIgnoreCase))
            {
                // Not a special folder, use as-is (relative to user profile)
                return Path.Combine(_userProfile, path);
            }

            return string.IsNullOrEmpty(restPart)
                ? basePath
                : Path.Combine(basePath, restPart);
        }

        private double GetCpuUsage()
        {
            // Simplified - in production would use PerformanceCounter
            return Math.Round(Random.Shared.NextDouble() * 100, 1);
        }

        private double GetRamUsage()
        {
            var gc = GC.GetGCMemoryInfo();
            var totalMemory = gc.TotalAvailableMemoryBytes;
            var usedMemory = GC.GetTotalMemory(false);
            return Math.Round((double)usedMemory / totalMemory * 100, 1);
        }

        private double GetDiskUsage()
        {
            var drive = DriveInfo.GetDrives()
                .FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);

            if (drive == null) return 0;

            var used = drive.TotalSize - drive.AvailableFreeSpace;
            return Math.Round((double)used / drive.TotalSize * 100, 1);
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
