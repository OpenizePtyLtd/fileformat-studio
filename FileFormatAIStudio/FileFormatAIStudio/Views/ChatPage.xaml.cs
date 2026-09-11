using System;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace FileFormatAIStudio.Views
{
    public sealed partial class ChatPage : Page
    {
        public static Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
        public static Visibility ToInvertedVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

        public ChatViewModel ViewModel { get; }

        public ChatPage()
        {
            InitializeComponent();
            ViewModel = ((App)Application.Current).Services.GetRequiredService<ChatViewModel>();
            ViewModel.MessageAdded += OnMessageAdded;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Guid sessionId)
            {
                await ViewModel.InitializeAsync(sessionId);
            }
            else
            {
                await ViewModel.InitializeAsync();
            }
        }

        private void OnMessageAdded()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                MessagesScrollViewer?.ChangeView(null, MessagesScrollViewer.ScrollableHeight, null, false);
            });
        }

        private async void OnPromptPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
                bool isShiftDown = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

                if (!isShiftDown)
                {
                    e.Handled = true;
                    if (ViewModel.SendMessageCommand.CanExecute(null))
                    {
                        await ViewModel.SendMessageCommand.ExecuteAsync(null);
                    }
                }
            }
        }

        private async void OnAvailableKbItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is KnowledgebaseEntity kb)
            {
                AttachKbFlyout?.Hide();
                await ViewModel.AttachKnowledgebaseCommand.ExecuteAsync(kb);
            }
        }

        private async void OnDetachKbClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is KnowledgebaseEntity kb)
            {
                await ViewModel.DetachKnowledgebaseCommand.ExecuteAsync(kb);
            }
        }
    }
}

