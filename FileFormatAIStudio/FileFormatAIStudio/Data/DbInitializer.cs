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
        public static async Task InitializeAsync(AppDbContext context)
        {
            // Baseline existing databases created via EnsureCreatedAsync prior to migrations
            await BaselineLegacyDatabaseIfNecessaryAsync(context);

            // Apply any pending EF Core migrations
            await context.Database.MigrateAsync();

            // Seed default providers if none exist
            if (!await context.Providers.AnyAsync())
            {
                var openAiProvider = new ProviderConfigEntity
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

                var openRouterProvider = new ProviderConfigEntity
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

                var localGptOssProvider = new ProviderConfigEntity
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

                context.Providers.AddRange(openAiProvider, openRouterProvider, localGptOssProvider);
                await context.SaveChangesAsync();
            }
        }

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

