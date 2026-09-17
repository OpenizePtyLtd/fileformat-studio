using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Benchmarking;
using Microsoft.EntityFrameworkCore;

namespace FileFormatAIStudio.Services.Parsing
{
    /// <summary>
    /// Implements IDocumentEnginePreferenceService backed by SQLite AppSettings and BenchmarkSessions.
    /// </summary>
    public class DocumentEnginePreferenceService : IDocumentEnginePreferenceService
    {
        private readonly AppDbContext _context;
        private readonly IDocumentCategoryRegistry _categoryRegistry;
        private readonly IDocumentParserFactory _parserFactory;

        public const string AutoEngineId = "Auto";

        public DocumentEnginePreferenceService(
            AppDbContext context,
            IDocumentCategoryRegistry categoryRegistry,
            IDocumentParserFactory parserFactory)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _categoryRegistry = categoryRegistry ?? throw new ArgumentNullException(nameof(categoryRegistry));
            _parserFactory = parserFactory ?? throw new ArgumentNullException(nameof(parserFactory));
        }

        public async Task<string> GetPreferredEngineIdAsync(DocumentCategory category, CancellationToken cancellationToken = default)
        {
            string key = GetPreferenceSettingKey(category);
            var setting = await _context.AppSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

            return !string.IsNullOrWhiteSpace(setting?.Value) ? setting.Value : AutoEngineId;
        }

        public async Task SetPreferredEngineIdAsync(DocumentCategory category, string engineId, CancellationToken cancellationToken = default)
        {
            string key = GetPreferenceSettingKey(category);
            string value = string.IsNullOrWhiteSpace(engineId) ? AutoEngineId : engineId.Trim();

            var setting = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

            if (setting == null)
            {
                setting = new AppSettingEntity
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.AppSettings.Add(setting);
            }
            else
            {
                setting.Value = value;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyDictionary<DocumentCategory, string>> GetAllPreferencesAsync(CancellationToken cancellationToken = default)
        {
            var categories = _categoryRegistry.GetAllCategories();
            var result = new Dictionary<DocumentCategory, string>();

            foreach (var category in categories)
            {
                result[category] = await GetPreferredEngineIdAsync(category, cancellationToken);
            }

            return result;
        }

        public async Task<IDocumentParser?> ResolveParserForDocumentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            var ext = DocumentParserFactory.NormalizeExtension(filePath);
            if (string.IsNullOrEmpty(ext))
                return null;

            var category = _categoryRegistry.ResolveCategory(filePath);
            var preferredEngineId = await GetPreferredEngineIdAsync(category, cancellationToken);

            // 1. If Auto: check if benchmark winner exists and supports format
            if (string.Equals(preferredEngineId, AutoEngineId, StringComparison.OrdinalIgnoreCase))
            {
                var winnerEngineId = await GetBenchmarkWinnerEngineIdAsync(category, cancellationToken);
                if (!string.IsNullOrWhiteSpace(winnerEngineId))
                {
                    var winnerParser = _parserFactory.GetParser(winnerEngineId);
                    if (winnerParser != null && winnerParser.IsAvailable && winnerParser.SupportedExtensions.Contains(ext))
                    {
                        return winnerParser;
                    }
                }

                // Fall back to factory priority resolution
                return _parserFactory.ResolveParser(filePath);
            }

            // 2. Specific engine configured: verify availability & format support
            var specificParser = _parserFactory.GetParser(preferredEngineId);
            if (specificParser != null && specificParser.IsAvailable && specificParser.SupportedExtensions.Contains(ext))
            {
                return specificParser;
            }

            // 3. Fall back gracefully to best available parser if specific engine does not support this exact format
            return _parserFactory.ResolveParser(filePath);
        }

        public async Task<string?> GetBenchmarkWinnerEngineIdAsync(DocumentCategory category, CancellationToken cancellationToken = default)
        {
            string key = GetWinnerSettingKey(category);
            var setting = await _context.AppSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

            if (!string.IsNullOrWhiteSpace(setting?.Value))
            {
                return setting.Value;
            }

            // Fallback: Query latest BenchmarkSession for this category in the database
            return await DiscoverWinnerFromPastSessionsAsync(category, cancellationToken);
        }

        public async Task<string?> GetBenchmarkWinnerDisplayNameAsync(DocumentCategory category, CancellationToken cancellationToken = default)
        {
            string key = GetWinnerNameSettingKey(category);
            var setting = await _context.AppSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

            if (!string.IsNullOrWhiteSpace(setting?.Value))
            {
                return setting.Value;
            }

            // Fallback: If engine ID was discovered, check parser DisplayName
            var winnerId = await GetBenchmarkWinnerEngineIdAsync(category, cancellationToken);
            if (!string.IsNullOrWhiteSpace(winnerId))
            {
                var parser = _parserFactory.GetParser(winnerId);
                return parser?.DisplayName;
            }

            return null;
        }

        public async Task RecordBenchmarkWinnerAsync(
            DocumentCategory category,
            string engineId,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(engineId))
                return;

            string idKey = GetWinnerSettingKey(category);
            string nameKey = GetWinnerNameSettingKey(category);

            await UpsertSettingAsync(idKey, engineId.Trim(), cancellationToken);
            await UpsertSettingAsync(nameKey, displayName.Trim(), cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<string?> DiscoverWinnerFromPastSessionsAsync(DocumentCategory category, CancellationToken cancellationToken)
        {
            string categoryStr = category.ToString();
            var latestSession = await _context.BenchmarkSessions
                .Include(s => s.Documents)
                    .ThenInclude(d => d.RunResults)
                .Where(s => s.Category == categoryStr)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestSession == null || latestSession.Documents == null)
                return null;

            var winnerGroup = latestSession.Documents
                .SelectMany(d => d.RunResults)
                .Where(r => r.Status == "Success" && r.Rank == 1)
                .GroupBy(r => new { r.EngineId, r.EngineDisplayName })
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Average(r => r.OverallScore))
                .FirstOrDefault();

            if (winnerGroup == null)
                return null;

            string winnerEngineId = winnerGroup.Key.EngineId;
            string winnerDisplayName = winnerGroup.Key.EngineDisplayName;

            // Cache discovered winner in settings for future instantaneous lookups
            await UpsertSettingAsync(GetWinnerSettingKey(category), winnerEngineId, cancellationToken);
            await UpsertSettingAsync(GetWinnerNameSettingKey(category), winnerDisplayName, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return winnerEngineId;
        }

        private async Task UpsertSettingAsync(string key, string value, CancellationToken cancellationToken)
        {
            var existing = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
            if (existing == null)
            {
                _context.AppSettings.Add(new AppSettingEntity
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        private static string GetPreferenceSettingKey(DocumentCategory category) =>
            $"DocumentEnginePreference_{category}";

        private static string GetWinnerSettingKey(DocumentCategory category) =>
            $"BenchmarkWinner_{category}";

        private static string GetWinnerNameSettingKey(DocumentCategory category) =>
            $"BenchmarkWinnerName_{category}";
    }
}

