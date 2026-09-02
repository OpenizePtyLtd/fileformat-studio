using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace FileFormatAIStudio.ViewModels
{
    public partial class ChatMessageItemViewModel : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsUser))]
        [NotifyPropertyChangedFor(nameof(IsAssistant))]
        [NotifyPropertyChangedFor(nameof(UserVisibility))]
        [NotifyPropertyChangedFor(nameof(AssistantVisibility))]
        private string _role = "User";

        [ObservableProperty]
        private string _content = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TimeString))]
        private DateTime _timestamp = DateTime.Now;

        public bool IsUser => string.Equals(Role, "User", StringComparison.OrdinalIgnoreCase);
        public bool IsAssistant => !IsUser;

        public Visibility UserVisibility => IsUser ? Visibility.Visible : Visibility.Collapsed;
        public Visibility AssistantVisibility => IsAssistant ? Visibility.Visible : Visibility.Collapsed;

        public string TimeString => Timestamp.ToShortTimeString();
    }
}

