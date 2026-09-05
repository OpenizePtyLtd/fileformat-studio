using System;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Chat;
using FileFormatAIStudio.Services.Settings;
using FileFormatAIStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace FileFormatAIStudio
{
    public partial class App : Application
    {
        private Window? _window;

        public IServiceProvider Services { get; }

        public App()
        {
            InitializeComponent();
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Database
            services.AddDbContext<AppDbContext>();

            // Services
            services.AddSingleton<IAIClientFactory, AIClientFactory>();
            services.AddScoped<ISettingsService, SettingsService>();
            services.AddScoped<IChatSessionService, ChatSessionService>();
            services.AddScoped<IChatExecutionService, ChatExecutionService>();

            // ViewModels
            services.AddTransient<HomeViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<ChatViewModel>();
            services.AddTransient<SettingsViewModel>();

            return services.BuildServiceProvider();
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Initialize Database Schema & Seed Data
            using (var scope = Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await DbInitializer.InitializeAsync(dbContext);
            }

            _window = new MainWindow();
            _window.Activate();
        }
    }
}

