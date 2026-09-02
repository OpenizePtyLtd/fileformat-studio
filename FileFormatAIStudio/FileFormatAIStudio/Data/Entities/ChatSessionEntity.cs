using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileFormatAIStudio.Data.Entities
{
    public partial class ChatSessionEntity : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ObservableProperty]
        private string _title = "New Chat";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public Guid? SelectedModelId { get; set; }

        public List<ChatMessageEntity> Messages { get; set; } = new();
    }
}

