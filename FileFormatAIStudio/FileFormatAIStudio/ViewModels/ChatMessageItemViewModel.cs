using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using FileFormatAIStudio.Data.Entities;
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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCitations))]
        [NotifyPropertyChangedFor(nameof(CitationsVisibility))]
        [NotifyPropertyChangedFor(nameof(CitationsHeader))]
        private ObservableCollection<CitationReference> _citations = new();

        public bool IsUser => string.Equals(Role, "User", StringComparison.OrdinalIgnoreCase);
        public bool IsAssistant => !IsUser;

        public Visibility UserVisibility => IsUser ? Visibility.Visible : Visibility.Collapsed;
        public Visibility AssistantVisibility => IsAssistant ? Visibility.Visible : Visibility.Collapsed;

        public bool HasCitations => Citations.Count > 0;
        public Visibility CitationsVisibility => HasCitations ? Visibility.Visible : Visibility.Collapsed;
        public string CitationsHeader => $"Sources ({Citations.Count})";

        public string TimeString => Timestamp.ToShortTimeString();

        public void LoadCitations(IEnumerable<CitationReference> citations)
        {
            Citations.Clear();
            foreach (var c in citations)
            {
                Citations.Add(c);
            }
            OnPropertyChanged(nameof(HasCitations));
            OnPropertyChanged(nameof(CitationsVisibility));
            OnPropertyChanged(nameof(CitationsHeader));
        }

        public void LoadCitationsFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                var list = JsonSerializer.Deserialize<List<CitationReference>>(json);
                if (list != null)
                {
                    LoadCitations(list);
                }
            }
            catch
            {
                // Ignore corrupt/unparseable JSON
            }
        }
    }
}

