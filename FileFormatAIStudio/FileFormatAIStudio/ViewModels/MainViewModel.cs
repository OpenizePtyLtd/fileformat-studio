using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Chat;

namespace FileFormatAIStudio.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IChatSessionService _sessionService;

        [ObservableProperty]
        private ObservableCollection<ChatSessionEntity> _sessions = new();

        [ObservableProperty]
        private ChatSessionEntity? _selectedSession;

        public event Action<Guid>? SessionSelected;

        public MainViewModel(IChatSessionService sessionService)
        {
            _sessionService = sessionService;
        }

        [RelayCommand]
        public async Task LoadSessionsAsync()
        {
            var list = await _sessionService.GetSessionsAsync();
            Sessions.Clear();
            foreach (var s in list)
            {
                Sessions.Add(s);
            }

            if (SelectedSession == null && Sessions.Count > 0)
            {
                SelectedSession = Sessions[0];
            }
        }

        partial void OnSelectedSessionChanged(ChatSessionEntity? value)
        {
            if (value != null)
            {
                SessionSelected?.Invoke(value.Id);
            }
        }

        [RelayCommand]
        public async Task CreateNewSessionAsync()
        {
            // Check if there is already an existing session with 0 messages
            var existingEmpty = Sessions.FirstOrDefault(s => s.Messages == null || s.Messages.Count == 0);
            if (existingEmpty != null)
            {
                SelectedSession = existingEmpty;
                return;
            }

            var newSession = await _sessionService.CreateSessionAsync("New Chat");
            var inList = Sessions.FirstOrDefault(s => s.Id == newSession.Id);
            if (inList == null)
            {
                Sessions.Insert(0, newSession);
                SelectedSession = newSession;
            }
            else
            {
                SelectedSession = inList;
            }
        }

        [RelayCommand]
        public async Task DeleteSessionAsync(ChatSessionEntity? session)
        {
            var target = session ?? SelectedSession;
            if (target == null) return;

            await _sessionService.DeleteSessionAsync(target.Id);
            Sessions.Remove(target);

            if (SelectedSession == target)
            {
                SelectedSession = Sessions.Count > 0 ? Sessions[0] : null;
                if (SelectedSession == null)
                {
                    await CreateNewSessionAsync();
                }
            }
        }

        [RelayCommand]
        public async Task RenameSessionAsync((Guid SessionId, string NewTitle) param)
        {
            await _sessionService.UpdateSessionTitleAsync(param.SessionId, param.NewTitle);
            var session = Sessions.FirstOrDefault(s => s.Id == param.SessionId);
            if (session != null)
            {
                session.Title = param.NewTitle;
            }
        }
    }
}

