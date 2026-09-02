using System;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            InitializeComponent();
            ViewModel = ((App)Application.Current).Services.GetRequiredService<SettingsViewModel>();
            this.Loaded += async (s, e) => await ViewModel.LoadProvidersAsync();
        }

        private async void OnDeleteModelClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ModelConfigEntity model)
            {
                await ViewModel.DeleteModelCommand.ExecuteAsync(model);
            }
        }
    }
}

