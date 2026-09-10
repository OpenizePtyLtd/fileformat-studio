using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FileFormatAIStudio.Services.Settings
{
    public class SettingsService : ISettingsService
    {
        private readonly AppDbContext _context;

        public SettingsService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProviderConfigEntity>> GetProvidersAsync()
        {
            var providers = await _context.Providers
                .Include(p => p.Models)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            // Deduplicate models in memory if any accidental duplicates exist in the database
            foreach (var provider in providers)
            {
                if (provider.Models != null && provider.Models.Count > 1)
                {
                    provider.Models = provider.Models
                        .GroupBy(m => m.ModelId.Trim().ToLowerInvariant())
                        .Select(g => g.First())
                        .ToList();
                }
            }

            return providers;
        }

        public async Task<ProviderConfigEntity?> GetProviderByIdAsync(Guid providerId)
        {
            var provider = await _context.Providers
                .Include(p => p.Models)
                .FirstOrDefaultAsync(p => p.Id == providerId);

            if (provider?.Models != null && provider.Models.Count > 1)
            {
                provider.Models = provider.Models
                    .GroupBy(m => m.ModelId.Trim().ToLowerInvariant())
                    .Select(g => g.First())
                    .ToList();
            }

            return provider;
        }

        public async Task SaveProviderAsync(ProviderConfigEntity provider)
        {
            var existing = await _context.Providers.FirstOrDefaultAsync(p => p.Id == provider.Id);
            if (existing == null)
            {
                _context.Providers.Add(provider);
            }
            else
            {
                existing.Name = provider.Name;
                existing.ProviderType = provider.ProviderType;
                existing.EndpointUrl = provider.EndpointUrl;
                existing.ApiKey = provider.ApiKey;
                existing.IsEnabled = provider.IsEnabled;
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteProviderAsync(Guid providerId)
        {
            var existing = await _context.Providers.FirstOrDefaultAsync(p => p.Id == providerId);
            if (existing != null)
            {
                _context.Providers.Remove(existing);
                await _context.SaveChangesAsync();
            }
        }

        public async Task AddModelAsync(ModelConfigEntity model)
        {
            string cleanId = model.ModelId.Trim().ToLowerInvariant();
            bool alreadyExists = await _context.Models.AnyAsync(m =>
                m.ProviderId == model.ProviderId &&
                m.ModelId.ToLower() == cleanId);

            if (alreadyExists)
            {
                return;
            }

            _context.Models.Add(model);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteModelAsync(Guid modelId)
        {
            var existing = await _context.Models.FirstOrDefaultAsync(m => m.Id == modelId);
            if (existing != null)
            {
                // Remove existing and any duplicates of this model under this provider
                var allMatches = await _context.Models
                    .Where(m => m.ProviderId == existing.ProviderId && m.ModelId.ToLower() == existing.ModelId.ToLower())
                    .ToListAsync();

                _context.Models.RemoveRange(allMatches);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<ModelConfigEntity>> GetAllEnabledModelsAsync()
        {
            return await _context.Models
                .Include(m => m.Provider)
                .Where(m => m.Provider != null && m.Provider.IsEnabled && !m.IsEmbeddingModel)
                .OrderBy(m => m.DisplayName)
                .ToListAsync();
        }

        public async Task RestoreDefaultProvidersAsync()
        {
            await DbInitializer.SeedDefaultProvidersAsync(_context);
        }
    }
}

