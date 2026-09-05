using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FileFormatAIStudio.Data
{
    public static class DbInitializer
    {
        public const string HasSeededDefaultProvidersKey = "HasSeededDefaultProviders";

        public static async Task InitializeAsync(AppDbContext context)
        {
            // Baseline existing databases created via EnsureCreatedAsync prior to migrations
            await BaselineLegacyDatabaseIfNecessaryAsync(context);

            // Apply any pending EF Core migrations
            await context.Database.MigrateAsync();

            // Check if default seeding has already taken place in history
            var seedSetting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == HasSeededDefaultProvidersKey);

            if (seedSetting == null)
            {
                // If setting is not present, check if this is an existing database with providers or a fresh installation
                if (await context.Providers.AnyAsync())
                {
                    // Existing database: mark as seeded so future deletions are not re-seeded
                    context.AppSettings.Add(new AppSettingEntity
                    {
                        Key = HasSeededDefaultProvidersKey,
                        Value = "true",
                        UpdatedAt = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();
                }
                else
                {
                    // Fresh installation: seed defaults
                    await SeedDefaultProvidersAsync(context);
                }
            }
            else if (bool.TryParse(seedSetting.Value, out var hasSeeded) && !hasSeeded)
            {
                // Explicitly unseeded flag
                await SeedDefaultProvidersAsync(context);
            }
            // If seedSetting.Value is "true", do not re-seed even if Providers table is empty
        }

        public static async Task SeedDefaultProvidersAsync(AppDbContext context)
        {
            var existingNames = await context.Providers
                .Select(p => p.Name.ToLower())
                .ToListAsync();

            var providersToAdd = new List<ProviderConfigEntity>();

            if (!existingNames.Contains("openai"))
            {
                providersToAdd.Add(CreateDefaultOpenAiProvider());
            }

            if (!existingNames.Contains("openrouter"))
            {
                providersToAdd.Add(CreateDefaultOpenRouterProvider());
            }

            if (!existingNames.Contains("local infrastructure (gptoss)"))
            {
                providersToAdd.Add(CreateDefaultLocalGptOssProvider());
            }

            if (providersToAdd.Count > 0)
            {
                context.Providers.AddRange(providersToAdd);
            }

            var setting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == HasSeededDefaultProvidersKey);
            if (setting == null)
            {
                context.AppSettings.Add(new AppSettingEntity
                {
                    Key = HasSeededDefaultProvidersKey,
                    Value = "true",
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                setting.Value = "true";
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();
        }

        public static ProviderConfigEntity CreateDefaultOpenAiProvider() => new()
        {
            Name = "OpenAI",
            ProviderType = "OpenAI",
            EndpointUrl = "https://api.openai.com/v1",
            ApiKey = string.Empty,
            IsEnabled = true,
            Models = new List<ModelConfigEntity>
            {
                new() { ModelId = "gpt-4o", DisplayName = "GPT-4o", IsDefault = true },
                new() { ModelId = "gpt-4o-mini", DisplayName = "GPT-4o Mini", IsDefault = false }
            }
        };

        public static ProviderConfigEntity CreateDefaultOpenRouterProvider() => new()
        {
            Name = "OpenRouter",
            ProviderType = "OpenRouter",
            EndpointUrl = "https://openrouter.ai/api/v1",
            ApiKey = string.Empty,
            IsEnabled = true,
            Models = new List<ModelConfigEntity>
            {
                new() { ModelId = "openai/gpt-4o-mini", DisplayName = "OpenRouter: GPT-4o Mini", IsDefault = false },
                new() { ModelId = "anthropic/claude-3.5-sonnet", DisplayName = "OpenRouter: Claude 3.5 Sonnet", IsDefault = false },
                new() { ModelId = "meta-llama/llama-3.2-3b-instruct:free", DisplayName = "OpenRouter: Llama 3.2 3B (Free)", IsDefault = false }
            }
        };

        public static ProviderConfigEntity CreateDefaultLocalGptOssProvider() => new()
        {
            Name = "Local Infrastructure (gptoss)",
            ProviderType = "Custom",
            EndpointUrl = "http://localhost:8000/v1",
            ApiKey = "not-needed",
            IsEnabled = true,
            Models = new List<ModelConfigEntity>
            {
                new() { ModelId = "gptoss", DisplayName = "Local gptoss Model", IsDefault = false }
            }
        };

        private static async Task BaselineLegacyDatabaseIfNecessaryAsync(AppDbContext context)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                bool shouldClose = false;
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                    shouldClose = true;
                }

                try
                {
                    // Check if 'Providers' table already exists in the SQLite database
                    using var checkCmd = connection.CreateCommand();
                    checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Providers';";
                    var result = await checkCmd.ExecuteScalarAsync();
                    long tableCount = result is long l ? l : Convert.ToInt64(result);

                    if (tableCount > 0)
                    {
                        // Ensure __EFMigrationsHistory table exists and baseline InitialCreate
                        using var baselineCmd = connection.CreateCommand();
                        baselineCmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                                ""MigrationId"" TEXT NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY,
                                ""ProductVersion"" TEXT NOT NULL
                            );
                            INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                            VALUES ('20260905060012_InitialCreate', '10.0.11');";
                        await baselineCmd.ExecuteNonQueryAsync();
                    }
                }
                finally
                {
                    if (shouldClose)
                    {
                        await connection.CloseAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DbInitializer] Legacy database baseline check skipped: {ex.Message}");
            }
        }
    }
}

