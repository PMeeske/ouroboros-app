// <copyright file="QdrantAdminTool.Analysis.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Partial class containing query, diagnostic, and compression analysis operations.
/// </summary>
public sealed partial class QdrantAdminTool
{
    private async Task<Result<string, string>> GetStatusAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Qdrant Neuro-Symbolic Memory Status\n");

        try
        {
            var response = await _httpClient.GetAsync("/collections", ct);
            if (!response.IsSuccessStatusCode)
                return Result<string, string>.Failure("Cannot connect to Qdrant");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            sb.AppendLine($"**Connected to:** {_baseUrl}");
            sb.AppendLine($"**Total collections:** {collections.GetArrayLength()}\n");

            // Get current embedding dimension
            int? currentDim = null;
            if (_embedFunc != null)
            {
                try
                {
                    var testEmbed = await _embedFunc("test", ct);
                    currentDim = testEmbed.Length;
                    sb.AppendLine($"**Active embedding dimension:** {currentDim}");
                }
                catch (Exception) { }
            }

            sb.AppendLine("\n## Collections\n");
            sb.AppendLine("| Collection | Points | Dimension | Status |");
            sb.AppendLine("|------------|--------|-----------|--------|");

            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString() ?? "unknown";
                var info = await GetCollectionInfoAsync(name, ct);

                string status = "✓ OK";
                if (currentDim.HasValue && info.Dimension > 0 && info.Dimension != currentDim.Value)
                    status = $"⚠ Dim mismatch ({info.Dimension}→{currentDim})";
                else if (info.Points == 0)
                    status = "○ Empty";

                sb.AppendLine($"| {name} | {info.Points:N0} | {info.Dimension} | {status} |");
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Status check failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> DiagnoseAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Qdrant Diagnostic Report\n");

        var issues = new List<string>();
        var recommendations = new List<string>();

        try
        {
            // Get current embedding dimension
            int? currentDim = null;
            if (_embedFunc != null)
            {
                try
                {
                    var testEmbed = await _embedFunc("test", ct);
                    currentDim = testEmbed.Length;
                }
                catch (Exception ex)
                {
                    issues.Add($"Embedding function error: {ex.Message}");
                    recommendations.Add("Check embedding model configuration");
                }
            }
            else
            {
                issues.Add("No embedding function configured");
                recommendations.Add("Configure embedding model for semantic search");
            }

            // Check collections
            var response = await _httpClient.GetAsync("/collections", ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            int totalPoints = 0;
            int emptyCollections = 0;
            int dimensionMismatches = 0;
            var ouroborosCollections = new List<string>();

            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString() ?? "";
                if (!name.StartsWith("ouroboros_")) continue;

                ouroborosCollections.Add(name);
                var info = await GetCollectionInfoAsync(name, ct);
                totalPoints += info.Points;

                if (info.Points == 0)
                {
                    emptyCollections++;
                }

                if (currentDim.HasValue && info.Dimension > 0 && info.Dimension != currentDim.Value)
                {
                    dimensionMismatches++;
                    issues.Add($"Collection '{name}' has dimension {info.Dimension}, but current model uses {currentDim}");
                    recommendations.Add($"Run: fix {name}");
                }
            }

            sb.AppendLine($"**Ouroboros Collections:** {ouroborosCollections.Count}");
            sb.AppendLine($"**Total Points:** {totalPoints:N0}");
            sb.AppendLine($"**Empty Collections:** {emptyCollections}");
            sb.AppendLine($"**Dimension Mismatches:** {dimensionMismatches}\n");

            if (issues.Count == 0)
            {
                sb.AppendLine("## ✓ No Issues Found\n");
                sb.AppendLine("Neuro-symbolic memory is healthy.");
            }
            else
            {
                sb.AppendLine("## ⚠ Issues Found\n");
                foreach (var issue in issues)
                    sb.AppendLine($"- {issue}");

                sb.AppendLine("\n## Recommendations\n");
                foreach (var rec in recommendations)
                    sb.AppendLine($"- {rec}");
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Diagnosis failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> GetStatsAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Neuro-Symbolic Memory Statistics\n");

        try
        {
            var response = await _httpClient.GetAsync("/collections", ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            long totalPoints = 0;
            var categoryStats = new Dictionary<string, (int collections, long points)>();

            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString() ?? "";
                if (!name.StartsWith("ouroboros_")) continue;

                var info = await GetCollectionInfoAsync(name, ct);
                totalPoints += info.Points;

                // Categorize by type
                var category = name.Replace("ouroboros_", "").Split('_')[0];
                if (!categoryStats.ContainsKey(category))
                    categoryStats[category] = (0, 0);
                categoryStats[category] = (categoryStats[category].collections + 1, categoryStats[category].points + info.Points);
            }

            sb.AppendLine($"**Total Points:** {totalPoints:N0}");
            sb.AppendLine($"**Categories:** {categoryStats.Count}\n");

            sb.AppendLine("## By Category\n");
            sb.AppendLine("| Category | Collections | Points |");
            sb.AppendLine("|----------|-------------|--------|");

            foreach (var (cat, stats) in categoryStats.OrderByDescending(x => x.Value.points))
            {
                sb.AppendLine($"| {cat} | {stats.collections} | {stats.points:N0} |");
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Stats failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> ListCollectionsAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();

        try
        {
            var response = await _httpClient.GetAsync("/collections", ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString() ?? "";
                if (name.StartsWith("ouroboros_"))
                {
                    var info = await GetCollectionInfoAsync(name, ct);
                    sb.AppendLine($"- {name}: {info.Points} points (dim={info.Dimension})");
                }
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"List failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> AnalyzeCompressionAsync(string? collection, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Fourier Vector Compression Analysis\n");

        try
        {
            // Get all collections if none specified
            var response = await _httpClient.GetAsync("/collections", ct);
            if (!response.IsSuccessStatusCode)
                return Result<string, string>.Failure("Cannot connect to Qdrant");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            var collectionsToAnalyze = new List<string>();
            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString()!;
                if (collection == null || name.Equals(collection, StringComparison.OrdinalIgnoreCase))
                {
                    collectionsToAnalyze.Add(name);
                }
            }

            if (collectionsToAnalyze.Count == 0)
            {
                return Result<string, string>.Failure($"Collection '{collection}' not found");
            }

            var compressionConfig = new Ouroboros.Domain.VectorCompression.CompressionConfig(128, 0.95);
            long totalOriginalBytes = 0;
            long totalPotentialBytes = 0;

            foreach (var colName in collectionsToAnalyze)
            {
                // Get collection info
                var infoResponse = await _httpClient.GetAsync($"/collections/{colName}", ct);
                if (!infoResponse.IsSuccessStatusCode) continue;

                var info = await infoResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
                var result = info.GetProperty("result");
                var config = result.GetProperty("config");
                var vectorConfig = config.GetProperty("params");
                int dimension = vectorConfig.GetProperty("size").GetInt32();
                int pointCount = result.GetProperty("points_count").GetInt32();

                sb.AppendLine($"## Collection: `{colName}`");
                sb.AppendLine($"- Current dimension: **{dimension}**");
                sb.AppendLine($"- Point count: **{pointCount:N0}**");

                // Sample some vectors for compression analysis
                if (pointCount > 0)
                {
                    var vectors = await SampleVectorsAsync(colName, Math.Min(100, pointCount), ct);

                    if (vectors.Count > 0)
                    {
                        // Analyze compression potential
                        var previewResult = Ouroboros.Domain.VectorCompression.VectorCompressionService.Preview(vectors[0], compressionConfig);

                        if (previewResult.IsFailure)
                        {
                            sb.AppendLine($"\n### Compression Analysis Error: {previewResult.Error}");
                            continue;
                        }

                        var preview = previewResult.Value;

                        sb.AppendLine($"\n### Compression Analysis (sampled {vectors.Count} vectors):");
                        sb.AppendLine($"- Original size per vector: **{preview.OriginalSizeBytes:N0} bytes**");
                        sb.AppendLine($"- DCT compressed: **{preview.DCTCompressedSize:N0} bytes** ({preview.DCTEnergyRetained:P1} energy retained)");
                        sb.AppendLine($"- FFT compressed: **{preview.FFTCompressedSize:N0} bytes** (ratio: {preview.FFTCompressionRatio:F1}x)");
                        sb.AppendLine($"- Quantized DCT: **{preview.QuantizedDCTSize:N0} bytes** (8-bit)");
                        sb.AppendLine($"- Best compression: **{preview.BestCompressionRatio:F1}x** using {preview.RecommendedMethod}");

                        // Calculate total savings
                        long origTotal = (long)pointCount * preview.OriginalSizeBytes;
                        long compTotal = (long)pointCount * preview.QuantizedDCTSize;

                        totalOriginalBytes += origTotal;
                        totalPotentialBytes += compTotal;

                        sb.AppendLine($"\n### Potential Savings for Collection:");
                        sb.AppendLine($"- Current storage: **{FormatBytes(origTotal)}**");
                        sb.AppendLine($"- Compressed storage: **{FormatBytes(compTotal)}**");
                        sb.AppendLine($"- Savings: **{FormatBytes(origTotal - compTotal)}** ({(1 - (double)compTotal / origTotal):P1} reduction)");

                        // Spectral analysis
                        var spectralInfo = AnalyzeSpectralContent(vectors);
                        sb.AppendLine($"\n### Spectral Profile:");
                        sb.AppendLine($"- Energy concentration: {spectralInfo.EnergyConcentration:P1} in first 25% of spectrum");
                        sb.AppendLine($"- Effective dimensionality: ~{spectralInfo.EffectiveDimension} (where 95% energy lives)");
                        sb.AppendLine($"- Recommended target dim: **{spectralInfo.RecommendedDimension}**");
                    }
                }

                sb.AppendLine();
            }

            if (totalOriginalBytes > 0)
            {
                sb.AppendLine("## Total Compression Summary");
                sb.AppendLine($"- Total current storage: **{FormatBytes(totalOriginalBytes)}**");
                sb.AppendLine($"- Total potential: **{FormatBytes(totalPotentialBytes)}**");
                sb.AppendLine($"- Total savings: **{FormatBytes(totalOriginalBytes - totalPotentialBytes)}**");
                sb.AppendLine($"- Overall compression: **{(double)totalOriginalBytes / totalPotentialBytes:F1}x**");
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Compression analysis failed: {ex.Message}");
        }
    }

    private async Task<List<float[]>> SampleVectorsAsync(string collection, int count, CancellationToken ct)
    {
        var vectors = new List<float[]>();

        try
        {
            var scrollRequest = new { limit = count, with_vector = true };
            var response = await _httpClient.PostAsJsonAsync($"/collections/{collection}/points/scroll", scrollRequest, ct);

            if (!response.IsSuccessStatusCode)
                return vectors;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var points = json.GetProperty("result").GetProperty("points");

            foreach (var point in points.EnumerateArray())
            {
                if (point.TryGetProperty("vector", out var vectorEl))
                {
                    var vec = new List<float>();
                    foreach (var v in vectorEl.EnumerateArray())
                    {
                        vec.Add(v.GetSingle());
                    }

                    if (vec.Count > 0)
                        vectors.Add(vec.ToArray());
                }
            }
        }
        catch
        {
            // Ignore sampling errors
        }

        return vectors;
    }

    private static (double EnergyConcentration, int EffectiveDimension, int RecommendedDimension) AnalyzeSpectralContent(List<float[]> vectors)
    {
        if (vectors.Count == 0 || vectors[0].Length == 0)
            return (0, 0, 64);

        int dim = vectors[0].Length;
        var dctCompressor = new Ouroboros.Domain.VectorCompression.DCTVectorCompressor(dim, 1.0);

        // Compute average energy distribution across DCT coefficients
        var avgEnergy = new double[dim];
        double totalEnergy = 0;

        foreach (var vec in vectors)
        {
            var compressed = dctCompressor.Compress(vec);
            for (int i = 0; i < compressed.Coefficients.Length && i < dim; i++)
            {
                double e = compressed.Coefficients[i] * compressed.Coefficients[i];
                avgEnergy[i] += e;
                totalEnergy += e;
            }
        }

        if (totalEnergy < double.Epsilon)
            return (0, dim, Math.Min(128, dim));

        // Normalize
        for (int i = 0; i < dim; i++)
            avgEnergy[i] /= totalEnergy;

        // Find energy concentration in first 25%
        int quarter = dim / 4;
        double energyInQuarter = avgEnergy.Take(quarter).Sum();

        // Find effective dimension (95% cumulative energy)
        double cumulative = 0;
        int effective = dim;
        for (int i = 0; i < dim; i++)
        {
            cumulative += avgEnergy[i];
            if (cumulative >= 0.95)
            {
                effective = i + 1;
                break;
            }
        }

        // Recommend dimension based on effective + safety margin
        int recommended = Math.Min(dim, Math.Max(64, (int)(effective * 1.1)));

        // Round to nice power-of-2-ish numbers
        int[] niceDims = { 64, 96, 128, 192, 256, 384, 512, 768, 1024 };
        recommended = niceDims.FirstOrDefault(d => d >= recommended);
        if (recommended == 0) recommended = dim;

        return (energyInQuarter, effective, recommended);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:F1} {sizes[order]}";
    }
}
