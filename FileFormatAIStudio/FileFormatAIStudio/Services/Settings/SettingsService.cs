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
            return await _context.Providers
                .Include(p => p.Models)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<ProviderConfigEntity?> GetProviderByIdAsync(Guid providerId)
        {
            return await _context.Providers
                .Include(p => p.Models)
                .FirstOrDefaultAsync(p => p.Id == providerId);
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
            _context.Models.Add(model);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteModelAsync(Guid modelId)
        {
            var existing = await _context.Models.FirstOrDefaultAsync(m => m.Id == modelId);
            if (existing != null)
            {
                _context.Models.Remove(existing);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<ModelConfigEntity>> GetAllEnabledModelsAsync()
        {
            return await _context.Models
                .Include(m => m.Provider)
                .Where(m => m.Provider != null && m.Provider.IsEnabled)
                .OrderBy(m => m.DisplayName)
                .ToListAsync();
        }

        public async Task RestoreDefaultProvidersAsync()
        {
            await DbInitializer.SeedDefaultProvidersAsync(_context);
        }
    }
}

