using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CrystalFrame.Dashboard
{
    /// <summary>
    /// Extracts Core.exe from embedded resources to %LOCALAPPDATA%\CrystalFrame\.
    /// Verifies hash to only update when version changes.
    /// </summary>
    public class CoreExtractor
    {
        private const string CoreResourceName = "CrystalFrame.Core.exe";
        private const string HashFileName = "CrystalFrame.Core.exe.hash";

        private readonly string _extractionDir;
        private readonly string _coreExePath;
        private readonly string _hashFilePath;

        public string CoreExePath => _coreExePath;

        public CoreExtractor()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _extractionDir = Path.Combine(appData, "CrystalFrame");
            _coreExePath = Path.Combine(_extractionDir, CoreResourceName);
            _hashFilePath = Path.Combine(_extractionDir, HashFileName);
        }

        /// <summary>
        /// Ensures Core.exe is extracted and up-to-date.
        /// Returns true if Core.exe is ready to use.
        /// </summary>
        public async Task<ExtractResult> EnsureExtractedAsync()
        {
            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(_extractionDir);

                // Get embedded resource
                var assembly = Assembly.GetExecutingAssembly();
                using var resourceStream = assembly.GetManifestResourceStream(CoreResourceName);

                if (resourceStream == null)
                {
                    Debug.WriteLine("Core.exe not embedded in assembly - using fallback path");
                    return new ExtractResult(true, "Using development path");
                }

                // Calculate hash of embedded resource
                var embeddedHash = await ComputeHashAsync(resourceStream);
                resourceStream.Position = 0; // Reset for extraction

                // Check if extraction needed
                bool needsExtraction = !File.Exists(_coreExePath) || !HashMatches(embeddedHash);

                if (!needsExtraction)
                {
                    Debug.WriteLine("Core.exe is up-to-date");
                    return new ExtractResult(true, "Already up-to-date");
                }

                // Stop existing Core process before updating
                if (File.Exists(_coreExePath))
                {
                    var stopped = await StopExistingCoreAsync();
                    if (!stopped)
                    {
                        return new ExtractResult(false, "Could not stop existing Core process");
                    }
                }

                // Extract with retry for locked file
                bool extracted = await ExtractWithRetryAsync(resourceStream);
                if (!extracted)
                {
                    return new ExtractResult(false, "Could not extract Core.exe - file may be locked");
                }

                // Save hash
                await File.WriteAllTextAsync(_hashFilePath, embeddedHash);

                Debug.WriteLine("Core.exe extracted successfully");
                return new ExtractResult(true, "Extracted new version");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Extraction error: {ex.Message}");
                return new ExtractResult(false, ex.Message);
            }
        }

        private async Task<string> ComputeHashAsync(Stream stream)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = await Task.Run(() => sha256.ComputeHash(stream));
            return Convert.ToHexString(hashBytes);
        }

        private bool HashMatches(string embeddedHash)
        {
            if (!File.Exists(_hashFilePath))
                return false;

            try
            {
                var savedHash = File.ReadAllText(_hashFilePath).Trim();
                return string.Equals(savedHash, embeddedHash, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> StopExistingCoreAsync()
        {
            try
            {
                var processes = Process.GetProcessesByName("CrystalFrame.Core");
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        await Task.Run(() => process.WaitForExit(3000));
                    }
                    catch
                    {
                        // Process may have already exited
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to stop Core: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExtractWithRetryAsync(Stream resourceStream)
        {
            const int maxRetries = 3;
            const int retryDelayMs = 500;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Delete existing file
                    if (File.Exists(_coreExePath))
                    {
                        File.Delete(_coreExePath);
                    }

                    // Extract to temp file first, then move (atomic operation)
                    var tempPath = _coreExePath + ".tmp";
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await resourceStream.CopyToAsync(fileStream);
                    }

                    // Move temp to final location
                    File.Move(tempPath, _coreExePath, overwrite: true);
                    return true;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    Debug.WriteLine($"Extraction attempt {i + 1} failed, retrying...");
                    await Task.Delay(retryDelayMs);
                    resourceStream.Position = 0;
                }
            }

            return false;
        }
    }

    public class ExtractResult
    {
        public bool Success { get; }
        public string Message { get; }

        public ExtractResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }
}
