using System;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.ViewModels;
using FileFormatAIStudio.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

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
            RootNavigationView.SelectedItem = null;
            ContentFrame.Navigate(typeof(ChatPage), sessionId);
        }

        private async void OnNewChatClicked(object sender, RoutedEventArgs e)
        {
            await ViewModel.CreateNewSessionAsync();
            if (ViewModel.SelectedSession != null)
            {
                RootNavigationView.SelectedItem = null;
                ContentFrame.Navigate(typeof(ChatPage), ViewModel.SelectedSession.Id);
            }
        }

        private void OnSessionItemClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatSessionEntity session)
            {
                ViewModel.SelectedSession = session;
                RootNavigationView.SelectedItem = null;
                ContentFrame.Navigate(typeof(ChatPage), session.Id);
            }
        }

        private void OnSessionSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SessionsListView.SelectedItem is ChatSessionEntity session)
            {
                RootNavigationView.SelectedItem = null;
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

        private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
            else if (ViewModel.SelectedSession != null)
            {
                RootNavigationView.SelectedItem = null;
                ContentFrame.Navigate(typeof(ChatPage), ViewModel.SelectedSession.Id);
            }
        }

        private void OnContentFrameNavigated(object sender, NavigationEventArgs e)
        {
            if (e.SourcePageType == typeof(SettingsPage))
            {
                RootNavigationView.SelectedItem = RootNavigationView.SettingsItem;
                RootNavigationView.IsBackButtonVisible = NavigationViewBackButtonVisible.Visible;
                RootNavigationView.IsBackEnabled = true;
            }
            else
            {
                RootNavigationView.SelectedItem = null;
                RootNavigationView.IsBackButtonVisible = ContentFrame.CanGoBack
                    ? NavigationViewBackButtonVisible.Visible
                    : NavigationViewBackButtonVisible.Collapsed;
                RootNavigationView.IsBackEnabled = ContentFrame.CanGoBack;
            }
        }
    }
}

