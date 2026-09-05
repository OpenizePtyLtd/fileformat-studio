using System;
using FileFormatAIStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio.Views
{
    public sealed partial class HomePage : Page
    {
        public HomeViewModel ViewModel { get; }

        public HomePage()
        {
            InitializeComponent();
            ViewModel = ((App)Application.Current).Services.GetRequiredService<HomeViewModel>();

            ViewModel.StartNewChatRequested += async () =>
            {
                var mainVm = ((App)Application.Current).Services.GetRequiredService<MainViewModel>();
                await mainVm.CreateNewSessionAsync();
                if (mainVm.SelectedSession != null && Frame != null)
                {
                    Frame.Navigate(typeof(ChatPage), mainVm.SelectedSession.Id);
                }
            };

            ViewModel.OpenSettingsRequested += () =>
            {
                if (Frame != null)
                {
                    Frame.Navigate(typeof(SettingsPage));
                }
            };
        }

        private void OnStartNewChatClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.StartNewChatCommand.Execute(null);
        }

        private void OnOpenSettingsClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenSettingsCommand.Execute(null);
        }
    }
}
