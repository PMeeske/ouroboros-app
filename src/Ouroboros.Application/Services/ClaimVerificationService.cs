// <copyright file="ClaimVerificationService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Collections.Concurrent;
using System.Text.Json;
using Ouroboros.Application.Tools.SystemTools;

/// <summary>
/// Interface for anti-hallucination claim verification and modification verification.
/// Verifies claims about the codebase and ensures modifications actually occur.
/// </summary>
public interface IClaimVerificationService
{
    /// <summary>
    /// Verify a claim about the codebase (file existence, file contents, modification history).
    /// </summary>
    /// <param name="claimArg">The claim to verify, either a JSON object or a file path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Verification result indicating whether the claim is valid.</returns>
    Task<ClaimVerification> VerifyClaimAsync(string claimArg, CancellationToken ct);

    /// <summary>
    /// Verify and execute a code modification with pre/post hash verification.
    /// </summary>
    /// <param name="modifyJson">JSON describing the modification (file, search, replace).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Verification result including whether the file was actually modified.</returns>
    Task<ModificationVerification> VerifyAndExecuteModificationAsync(string modifyJson, CancellationToken ct);

    /// <summary>
    /// Get anti-hallucination statistics for monitoring.
    /// </summary>
    AntiHallucinationStats GetAntiHallucinationStats();

    /// <summary>
    /// Records a hallucination event (verification failed).
    /// </summary>
    void RecordHallucination();

    /// <summary>
    /// Records a verified action event (verification passed).
    /// </summary>
    void RecordVerifiedAction();
}

/// <summary>
/// Service that provides anti-hallucination verification for autonomous actions.
/// Verifies file existence, file contents, and modification outcomes
/// to prevent the autonomous mind from acting on false beliefs.
/// </summary>
public class ClaimVerificationService : IClaimVerificationService
{
    private readonly ConcurrentDictionary<string, ModificationVerification> _pendingVerifications = new();
    private readonly ConcurrentQueue<ModificationVerification> _verificationHistory = new();
    private int _hallucinationCount;
    private int _verifiedActionCount;

    /// <summary>
    /// Delegate for verifying file existence. Returns true if file exists.
    /// </summary>
    public Func<string, bool>? VerifyFileExistsFunction { get; set; }

    /// <summary>
    /// Delegate for computing file hash. Returns hash string or null if file doesn't exist.
    /// </summary>
    public Func<string, string?>? ComputeFileHashFunction { get; set; }

    /// <summary>
    /// Delegate for executing tools.
    /// </summary>
    public Func<string, string, CancellationToken, Task<string>>? ExecuteToolFunction { get; set; }

    /// <inheritdoc/>
    public async Task<ClaimVerification> VerifyClaimAsync(string claimArg, CancellationToken ct)
    {
        try
        {
            // Try to parse as JSON first
            if (claimArg.TrimStart().StartsWith("{"))
            {
                var args = JsonSerializer.Deserialize<JsonElement>(claimArg);

                // Verify file existence claim
                if (args.TryGetProperty("file", out var fileProp))
                {
                    var filePath = fileProp.GetString();
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        var absolutePath = Path.IsPathRooted(filePath)
                            ? filePath
                            : Path.Combine(Environment.CurrentDirectory, filePath);

                        try { absolutePath = PathSanitizer.Sanitize(absolutePath); }
                        catch (UnauthorizedAccessException ex)
                        {
                            return new ClaimVerification { IsValid = false, Reason = $"Access denied: {ex.Message}", ClaimType = "file_existence" };
                        }

                        var exists = VerifyFileExistsFunction?.Invoke(absolutePath) ?? File.Exists(absolutePath);
                        return new ClaimVerification
                        {
                            IsValid = exists,
                            Reason = exists ? $"File exists: {filePath}" : $"File DOES NOT exist: {filePath}",
                            ClaimType = "file_existence"
                        };
                    }
                }

                // Verify file contains text claim
                if (args.TryGetProperty("file_contains", out var containsProp) &&
                    args.TryGetProperty("text", out var textProp))
                {
                    var filePath = containsProp.GetString();
                    var searchText = textProp.GetString();

                    if (!string.IsNullOrWhiteSpace(filePath) && !string.IsNullOrWhiteSpace(searchText))
                    {
                        var absolutePath = Path.IsPathRooted(filePath)
                            ? filePath
                            : Path.Combine(Environment.CurrentDirectory, filePath);

                        try { absolutePath = PathSanitizer.Sanitize(absolutePath); }
                        catch (UnauthorizedAccessException ex)
                        {
                            return new ClaimVerification { IsValid = false, Reason = $"Access denied: {ex.Message}", ClaimType = "file_contains" };
                        }

                        if (File.Exists(absolutePath))
                        {
                            var content = await File.ReadAllTextAsync(absolutePath, ct);
                            var contains = content.Contains(searchText);
                            return new ClaimVerification
                            {
                                IsValid = contains,
                                Reason = contains ? $"File contains the specified text" : $"File does NOT contain: {searchText[..Math.Min(50, searchText.Length)]}...",
                                ClaimType = "file_contains"
                            };
                        }
                        else
                        {
                            return new ClaimVerification
                            {
                                IsValid = false,
                                Reason = $"Cannot verify content - file does not exist: {filePath}",
                                ClaimType = "file_contains"
                            };
                        }
                    }
                }

                // Verify modification claim (check verification history)
                if (args.TryGetProperty("modification", out var modProp))
                {
                    var modPath = modProp.GetString();
                    var recentMod = _verificationHistory
                        .Where(v => v.FilePath?.Contains(modPath ?? "", StringComparison.OrdinalIgnoreCase) == true)
                        .OrderByDescending(v => v.AttemptedAt)
                        .FirstOrDefault();

                    if (recentMod != null)
                    {
                        return new ClaimVerification
                        {
                            IsValid = recentMod.WasVerified && recentMod.WasModified,
                            Reason = recentMod.WasModified
                                ? $"Modification verified at {recentMod.AttemptedAt:HH:mm:ss}"
                                : $"Modification NOT verified: {recentMod.Error ?? "unknown reason"}",
                            ClaimType = "modification"
                        };
                    }
                    else
                    {
                        return new ClaimVerification
                        {
                            IsValid = false,
                            Reason = $"No modification record found for: {modPath}",
                            ClaimType = "modification"
                        };
                    }
                }
            }

            // Simple file path check (non-JSON)
            if (!string.IsNullOrWhiteSpace(claimArg) && !claimArg.Contains(" "))
            {
                var absolutePath = Path.IsPathRooted(claimArg)
                    ? claimArg
                    : Path.Combine(Environment.CurrentDirectory, claimArg);

                try { absolutePath = PathSanitizer.Sanitize(absolutePath); }
                catch (UnauthorizedAccessException ex)
                {
                    return new ClaimVerification { IsValid = false, Reason = $"Access denied: {ex.Message}", ClaimType = "path_check" };
                }

                var exists = File.Exists(absolutePath);
                return new ClaimVerification
                {
                    IsValid = exists,
                    Reason = exists ? $"Path exists: {claimArg}" : $"Path DOES NOT exist: {claimArg}",
                    ClaimType = "path_check"
                };
            }

            return new ClaimVerification
            {
                IsValid = false,
                Reason = "Could not parse verification request",
                ClaimType = "unknown"
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return new ClaimVerification
            {
                IsValid = false,
                Reason = $"Verification error: {ex.Message}",
                ClaimType = "error"
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ModificationVerification> VerifyAndExecuteModificationAsync(string modifyJson, CancellationToken ct)
    {
        var verification = new ModificationVerification { AttemptedAt = DateTime.UtcNow };

        try
        {
            // Parse the modification request
            var args = JsonSerializer.Deserialize<JsonElement>(modifyJson);
            var filePath = args.TryGetProperty("file", out var fp) ? fp.GetString() : null;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return verification with { Error = "No file path specified in modification request" };
            }

            verification = verification with { FilePath = filePath };

            // Resolve to absolute path
            var absolutePath = Path.IsPathRooted(filePath)
                ? filePath
                : Path.Combine(Environment.CurrentDirectory, filePath);

            // CRITICAL: Verify file exists BEFORE attempting modification
            bool fileExists;
            if (VerifyFileExistsFunction != null)
            {
                fileExists = VerifyFileExistsFunction(absolutePath);
            }
            else
            {
                fileExists = File.Exists(absolutePath);
            }

            if (!fileExists)
            {
                var result = verification with
                {
                    Error = $"FILE DOES NOT EXIST: {filePath} - Cannot modify non-existent file. This would be a hallucination.",
                    FileExisted = false
                };
                _verificationHistory.Enqueue(result);
                return result;
            }

            verification = verification with { FileExisted = true };

            // Compute hash BEFORE modification
            var beforeHash = ComputeFileHashFunction?.Invoke(absolutePath) ?? ComputeSimpleHash(absolutePath);
            verification = verification with { BeforeHash = beforeHash };

            // Execute the actual modification
            if (ExecuteToolFunction != null)
            {
                var toolResult = await ExecuteToolFunction("modify_my_code", modifyJson, ct);

                // Compute hash AFTER modification
                var afterHash = ComputeFileHashFunction?.Invoke(absolutePath) ?? ComputeSimpleHash(absolutePath);

                // VERIFICATION: Did the file actually change?
                var wasModified = beforeHash != afterHash;

                verification = verification with
                {
                    ToolResult = toolResult,
                    AfterHash = afterHash,
                    WasModified = wasModified,
                    WasVerified = true,
                    Error = wasModified ? null : "Modification tool returned success but file hash unchanged - modification may not have occurred"
                };
            }
            else
            {
                verification = verification with { Error = "ExecuteToolFunction not available" };
            }
        }
        catch (JsonException jex)
        {
            verification = verification with { Error = $"Invalid JSON in modification request: {jex.Message}" };
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            verification = verification with { Error = $"Modification failed: {ex.Message}" };
        }

        _verificationHistory.Enqueue(verification);

        // Keep verification history bounded
        while (_verificationHistory.Count > 100)
        {
            _verificationHistory.TryDequeue(out _);
        }

        return verification;
    }

    /// <inheritdoc/>
    public AntiHallucinationStats GetAntiHallucinationStats() => new()
    {
        HallucinationCount = _hallucinationCount,
        VerifiedActionCount = _verifiedActionCount,
        PendingVerifications = _pendingVerifications.Count,
        RecentVerifications = _verificationHistory.ToList(),
        HallucinationRate = _verifiedActionCount + _hallucinationCount > 0
            ? (double)_hallucinationCount / (_verifiedActionCount + _hallucinationCount)
            : 0
    };

    /// <inheritdoc/>
    public void RecordHallucination()
    {
        Interlocked.Increment(ref _hallucinationCount);
    }

    /// <inheritdoc/>
    public void RecordVerifiedAction()
    {
        Interlocked.Increment(ref _verifiedActionCount);
    }

    /// <summary>
    /// Simple file hash computation fallback.
    /// </summary>
    private static string? ComputeSimpleHash(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            using var stream = File.OpenRead(filePath);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            return Convert.ToBase64String(hash);
        }
        catch
        {
            return null;
        }
    }
}
