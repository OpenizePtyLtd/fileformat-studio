using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Chat;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.Services.Settings;
using Microsoft.Extensions.AI;

namespace FileFormatAIStudio.ViewModels
{
    public partial class ChatViewModel : ObservableObject
    {
        private readonly IChatSessionService _sessionService;
        private readonly IChatExecutionService _chatExecutionService;
        private readonly ISettingsService _settingsService;
        private readonly IKnowledgebaseService _knowledgebaseService;

        private CancellationTokenSource? _cts;

        [ObservableProperty]
        private ChatSessionEntity? _currentSession;

        [ObservableProperty]
        private ObservableCollection<ChatMessageItemViewModel> _messages = new();

        [ObservableProperty]
        private ObservableCollection<ModelConfigEntity> _availableModels = new();

        [ObservableProperty]
        private ModelConfigEntity? _selectedModel;

        [ObservableProperty]
        private ObservableCollection<KnowledgebaseEntity> _attachedKnowledgebases = new();

        [ObservableProperty]
        private ObservableCollection<KnowledgebaseEntity> _availableKnowledgebasesToAttach = new();

        public bool HasAttachedKnowledgebases => AttachedKnowledgebases.Count > 0;
        public bool HasAvailableKnowledgebases => AvailableKnowledgebasesToAttach.Count > 0;
        public string AttachedKnowledgebasesCountText => AttachedKnowledgebases.Count == 1
            ? "1 knowledgebase attached"
            : $"{AttachedKnowledgebases.Count} knowledgebases attached";

        [ObservableProperty]
        private string _inputText = string.Empty;

        [ObservableProperty]
        private bool _isGenerating;

        [ObservableProperty]
        private string _statusText = "Ready";

        public event Action? MessageAdded;

        public ChatViewModel(
            IChatSessionService sessionService,
            IChatExecutionService chatExecutionService,
            ISettingsService settingsService,
            IKnowledgebaseService knowledgebaseService)
        {
            _sessionService = sessionService;
            _chatExecutionService = chatExecutionService;
            _settingsService = settingsService;
            _knowledgebaseService = knowledgebaseService;
        }

        public async Task InitializeAsync(Guid? sessionId = null)
        {
            await LoadModelsAsync();

            if (sessionId.HasValue)
            {
                await LoadSessionAsync(sessionId.Value);
            }
            else
            {
                var sessions = await _sessionService.GetSessionsAsync();
                if (sessions.Count > 0)
                {
                    await LoadSessionAsync(sessions[0].Id);
                }
                else
                {
                    var newSession = await _sessionService.CreateSessionAsync("New Chat");
                    await LoadSessionAsync(newSession.Id);
                }
            }
        }

        public async Task LoadModelsAsync()
        {
            var models = await _settingsService.GetAllEnabledModelsAsync();
            AvailableModels.Clear();
            foreach (var m in models)
            {
                AvailableModels.Add(m);
            }

            if (SelectedModel == null && AvailableModels.Count > 0)
            {
                SelectedModel = AvailableModels.FirstOrDefault(m => m.IsDefault) ?? AvailableModels[0];
            }
        }

        public async Task LoadSessionAsync(Guid sessionId)
        {
            CurrentSession = await _sessionService.GetSessionAsync(sessionId);
            Messages.Clear();

            if (CurrentSession != null)
            {
                if (CurrentSession.SelectedModelId.HasValue)
                {
                    var matchingModel = AvailableModels.FirstOrDefault(m => m.Id == CurrentSession.SelectedModelId.Value);
                    if (matchingModel != null)
                    {
                        SelectedModel = matchingModel;
                    }
                }

                foreach (var msg in CurrentSession.Messages)
                {
                    Messages.Add(new ChatMessageItemViewModel
                    {
                        Id = msg.Id,
                        Role = msg.Role,
                        Content = msg.Content,
                        Timestamp = msg.Timestamp
                    });
                }

                await RefreshKnowledgebasesAsync();
            }
            else
            {
                AttachedKnowledgebases.Clear();
                AvailableKnowledgebasesToAttach.Clear();
                UpdateKnowledgebaseVisibilityFlags();
            }

            MessageAdded?.Invoke();
        }

        partial void OnSelectedModelChanged(ModelConfigEntity? value)
        {
            if (value != null && CurrentSession != null)
            {
                _ = _sessionService.UpdateSessionModelAsync(CurrentSession.Id, value.Id);
            }
        }

        [RelayCommand]
        public async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText) || IsGenerating || CurrentSession == null)
            {
                return;
            }

            if (SelectedModel == null || SelectedModel.Provider == null)
            {
                StatusText = "Please select an enabled model in Settings first.";
                return;
            }

            string userPrompt = InputText.Trim();
            InputText = string.Empty;

            // 1. Save and display User Message
            var userMsgEntity = await _sessionService.AddMessageAsync(CurrentSession.Id, "User", userPrompt);
            CurrentSession.Messages.Add(userMsgEntity);
            if (CurrentSession.Title == "New Chat")
            {
                CurrentSession.Title = userPrompt.Length > 30 ? userPrompt.Substring(0, 30) + "..." : userPrompt;
            }

            var userMsgVm = new ChatMessageItemViewModel
            {
                Id = userMsgEntity.Id,
                Role = "User",
                Content = userPrompt,
                Timestamp = userMsgEntity.Timestamp
            };
            Messages.Add(userMsgVm);
            MessageAdded?.Invoke();

            // 2. Prepare assistant placeholder
            var assistantMsgVm = new ChatMessageItemViewModel
            {
                Role = "Assistant",
                Content = string.Empty,
                Timestamp = DateTime.Now
            };
            Messages.Add(assistantMsgVm);
            MessageAdded?.Invoke();

            // 3. Build History for Microsoft.Extensions.AI
            var history = new List<ChatMessage>();
            foreach (var m in Messages.Where(x => x != assistantMsgVm))
            {
                var role = m.IsUser ? ChatRole.User : ChatRole.Assistant;
                history.Add(new ChatMessage(role, m.Content));
            }

            // 4. Stream Response
            IsGenerating = true;
            StatusText = $"Generating response using {SelectedModel.DisplayName}...";
            _cts = new CancellationTokenSource();

            try
            {
                await foreach (var update in _chatExecutionService.StreamResponseAsync(
                    SelectedModel.Provider,
                    SelectedModel.ModelId,
                    history,
                    _cts.Token))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        assistantMsgVm.Content += update.Text;
                        MessageAdded?.Invoke();
                    }
                }

                StatusText = "Ready";
            }
            catch (OperationCanceledException)
            {
                StatusText = "Generation stopped.";
            }
            catch (Exception ex)
            {
                assistantMsgVm.Content += $"\n\n*[Error: {ex.Message}]*";
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsGenerating = false;
                _cts?.Dispose();
                _cts = null;

                // Save final assistant message to DB
                if (!string.IsNullOrWhiteSpace(assistantMsgVm.Content))
                {
                    var assistantEntity = await _sessionService.AddMessageAsync(CurrentSession.Id, "Assistant", assistantMsgVm.Content);
                    CurrentSession.Messages.Add(assistantEntity);
                }
            }
        }

        [RelayCommand]
        public void CancelGeneration()
        {
            if (IsGenerating && _cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        public void UpdateKnowledgebaseVisibilityFlags()
        {
            OnPropertyChanged(nameof(HasAttachedKnowledgebases));
            OnPropertyChanged(nameof(HasAvailableKnowledgebases));
            OnPropertyChanged(nameof(AttachedKnowledgebasesCountText));
        }

        public async Task RefreshKnowledgebasesAsync()
        {
            if (CurrentSession == null)
            {
                AttachedKnowledgebases.Clear();
                AvailableKnowledgebasesToAttach.Clear();
                UpdateKnowledgebaseVisibilityFlags();
                return;
            }

            var attached = await _sessionService.GetAttachedKnowledgebasesAsync(CurrentSession.Id);
            AttachedKnowledgebases.Clear();
            var attachedIds = new HashSet<Guid>();
            foreach (var kb in attached)
            {
                AttachedKnowledgebases.Add(kb);
                attachedIds.Add(kb.Id);
            }

            var allKbs = await _knowledgebaseService.GetKnowledgebasesAsync();
            AvailableKnowledgebasesToAttach.Clear();
            foreach (var kb in allKbs)
            {
                if (!attachedIds.Contains(kb.Id))
                {
                    AvailableKnowledgebasesToAttach.Add(kb);
                }
            }

            UpdateKnowledgebaseVisibilityFlags();
        }

        [RelayCommand]
        public async Task AttachKnowledgebaseAsync(KnowledgebaseEntity? kb)
        {
            if (kb == null || CurrentSession == null) return;

            await _sessionService.AttachKnowledgebaseAsync(CurrentSession.Id, kb.Id);

            var toRemove = AvailableKnowledgebasesToAttach.FirstOrDefault(x => x.Id == kb.Id);
            if (toRemove != null)
            {
                AvailableKnowledgebasesToAttach.Remove(toRemove);
            }

            if (!AttachedKnowledgebases.Any(x => x.Id == kb.Id))
            {
                AttachedKnowledgebases.Add(kb);
            }

            UpdateKnowledgebaseVisibilityFlags();
        }

        [RelayCommand]
        public async Task DetachKnowledgebaseAsync(KnowledgebaseEntity? kb)
        {
            if (kb == null || CurrentSession == null) return;

            await _sessionService.DetachKnowledgebaseAsync(CurrentSession.Id, kb.Id);

            var toRemove = AttachedKnowledgebases.FirstOrDefault(x => x.Id == kb.Id);
            if (toRemove != null)
            {
                AttachedKnowledgebases.Remove(toRemove);
            }

            if (!AvailableKnowledgebasesToAttach.Any(x => x.Id == kb.Id))
            {
                AvailableKnowledgebasesToAttach.Add(kb);
            }

            UpdateKnowledgebaseVisibilityFlags();
        }
    }
}

