using System;
using System.Collections.Generic;
using System.Linq;
using FileFormatAIStudio.Data.Entities;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.Views.Dialogs
{
    public sealed partial class DeleteProviderConfirmationDialog : ContentDialog
    {
        public DeleteProviderConfirmationDialog(ProviderConfigEntity provider, IEnumerable<ModelConfigEntity>? associatedModels = null)
        {
            InitializeComponent();

            ProviderNameTextBlock.Text = !string.IsNullOrWhiteSpace(provider.Name)
                ? provider.Name
                : "(Unnamed Provider)";

            EndpointUrlTextBlock.Text = !string.IsNullOrWhiteSpace(provider.EndpointUrl)
                ? provider.EndpointUrl
                : "(None)";

            if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            {
                ApiKeyStatusTextBlock.Text = "Configured (Stored securely via DPAPI)";
            }
            else
            {
                ApiKeyStatusTextBlock.Text = "None configured";
            }

            var models = (associatedModels ?? provider.Models)?.ToList() ?? new List<ModelConfigEntity>();
            if (models.Count > 0)
            {
                ModelsCountTextBlock.Text = $"{models.Count} model{(models.Count == 1 ? string.Empty : "s")} configured:";
                ModelsListControl.ItemsSource = models.Select(m =>
                    string.IsNullOrWhiteSpace(m.DisplayName) || string.Equals(m.DisplayName.Trim(), m.ModelId.Trim(), StringComparison.OrdinalIgnoreCase)
                        ? $"• {m.ModelId}"
                        : $"• {m.DisplayName} ({m.ModelId})"
                ).ToList();
            }
            else
            {
                ModelsCountTextBlock.Text = "No associated models.";
                ModelsListControl.ItemsSource = null;
            }
        }
    }
}

