using System;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FileFormatAIStudio.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _appName = "FileFormat Studio";

        [ObservableProperty]
        private string _appVersion;

        [ObservableProperty]
        private string _appDescription =
            "FileFormat Studio is an intelligent desktop workstation for file format automation, document processing, code generation, and multi-model AI workflows. Connect local or cloud AI models, manage chat sessions, and safeguard your credentials with Windows DPAPI encryption.";

        public event Action? StartNewChatRequested;
        public event Action? OpenSettingsRequested;

        public HomeViewModel()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            _appVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";
        }

        [RelayCommand]
        private void StartNewChat()
        {
            StartNewChatRequested?.Invoke();
        }

        [RelayCommand]
        private void OpenSettings()
        {
            OpenSettingsRequested?.Invoke();
        }
    }
}
