using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FileFormatAIStudio.Data.Entities;

namespace FileFormatAIStudio.Services.Settings
{
    public interface ISettingsService
    {
        Task<List<ProviderConfigEntity>> GetProvidersAsync();
        Task<ProviderConfigEntity?> GetProviderByIdAsync(Guid providerId);
        Task SaveProviderAsync(ProviderConfigEntity provider);
        Task DeleteProviderAsync(Guid providerId);

        Task AddModelAsync(ModelConfigEntity model);
        Task DeleteModelAsync(Guid modelId);
        Task<List<ModelConfigEntity>> GetAllEnabledModelsAsync();
    }
}

