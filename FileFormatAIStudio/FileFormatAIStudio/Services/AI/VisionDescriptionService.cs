using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace FileFormatAIStudio.Services.AI
{
    /// <summary>
    /// Service utilizing Microsoft.Extensions.AI to provide semantic vision descriptions of document diagrams and figures.
    /// Safely handles network errors and unsupported models by returning empty descriptions without interrupting document ingestion.
    /// </summary>
    public class VisionDescriptionService : IVisionDescriptionService
    {
        public const string VisionSettingKey = "vision.multimodal.enabled";

        private readonly AppDbContext _context;
        private readonly ISettingsService _settingsService;
        private readonly IAIClientFactory _clientFactory;

        public VisionDescriptionService(
            AppDbContext context,
            ISettingsService settingsService,
            IAIClientFactory clientFactory)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        }

        public async Task<bool> IsVisionEnabledAsync(CancellationToken cancellationToken = default)
        {
            var setting = await _context.AppSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == VisionSettingKey, cancellationToken);

            return setting?.Value != null &&
                   (setting.Value.Equals("true", StringComparison.OrdinalIgnoreCase) || setting.Value == "1");
        }

        public async Task<string> DescribeImageAsync(
            byte[] imageBytes,
            string mimeType = "image/png",
            string? contextHint = null,
            CancellationToken cancellationToken = default)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return string.Empty;

            bool isEnabled = await IsVisionEnabledAsync(cancellationToken);
            if (!isEnabled)
                return string.Empty;

            try
            {
                var models = await _settingsService.GetAllEnabledModelsAsync();
                var chatModel = models.FirstOrDefault(m => !m.IsEmbeddingModel);
                if (chatModel == null)
                    return string.Empty;

                var provider = await _settingsService.GetProviderByIdAsync(chatModel.ProviderId);
                if (provider == null || !provider.IsEnabled)
                    return string.Empty;

                var chatClient = _clientFactory.CreateChatClient(provider, chatModel.ModelId);

                string promptText = string.IsNullOrWhiteSpace(contextHint)
                    ? "Describe this diagram, chart, or figure concisely in 1-3 sentences focusing on key data points, relationships, and trends:"
                    : $"This image appears in context '{contextHint}'. Describe this diagram, chart, or figure concisely in 1-3 sentences focusing on key data points, relationships, and trends:";

                var contents = new List<AIContent>
                {
                    new TextContent(promptText),
                    new DataContent(imageBytes, mimeType)
                };

                var message = new ChatMessage(ChatRole.User, contents);
                var response = await chatClient.GetResponseAsync(new[] { message }, cancellationToken: cancellationToken);

                return response?.Text?.Trim() ?? string.Empty;
            }
            catch
            {
                // Soft-fail: if the model does not support multimodal vision, return empty without failing ingestion
                return string.Empty;
            }
        }
    }
}

