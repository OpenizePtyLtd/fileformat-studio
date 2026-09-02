using System;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.ViewModels;
using FileFormatAIStudio.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileFormatAIStudio
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = ((App)Application.Current).Services.GetRequiredService<MainViewModel>();

            ViewModel.SessionSelected += OnSessionSelectedFromViewModel;

            this.Activated += OnWindowActivated;
        }

        private async void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= OnWindowActivated;
            await ViewModel.LoadSessionsAsync();
            if (ViewModel.SelectedSession != null)
            {
                ContentFrame.Navigate(typeof(ChatPage), ViewModel.SelectedSession.Id);
            }
            else
            {
                await ViewModel.CreateNewSessionAsync();
            }
        }

        private void OnSessionSelectedFromViewModel(Guid sessionId)
        {
            ContentFrame.Navigate(typeof(ChatPage), sessionId);
        }

        private async void OnNewChatClicked(object sender, RoutedEventArgs e)
        {
            await ViewModel.CreateNewSessionAsync();
        }

        private void OnSessionSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SessionsListView.SelectedItem is ChatSessionEntity session)
            {
                ContentFrame.Navigate(typeof(ChatPage), session.Id);
            }
        }

        private async void OnDeleteSessionClicked(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ChatSessionEntity session)
            {
                await ViewModel.DeleteSessionCommand.ExecuteAsync(session);
            }
        }

        private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
        }
    }
}

