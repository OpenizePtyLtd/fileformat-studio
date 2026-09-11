using System;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Services.AI;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Benchmarking.Metrics;
using FileFormatAIStudio.Services.Chat;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.Services.Parsing;
using FileFormatAIStudio.Services.Settings;
using FileFormatAIStudio.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace FileFormatAIStudio
{
    public partial class App : Application
    {
        private Window? _window;

        public static Window? MainWindowInstance { get; private set; }

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

            // Document Parsing & Chunking
            services.AddSingleton<IAsposeLicenseService, AsposeLicenseService>();
            services.AddSingleton<IDocumentParser, AsposeDocumentParser>();
            services.AddSingleton<IDocumentParser, DotNetOssDocumentParser>();
            services.AddSingleton<IDocumentParser, PlainTextParser>();
            services.AddSingleton<IDocumentParserFactory, DocumentParserFactory>();
            services.AddSingleton<ITextChunker, TextChunker>();
            services.AddSingleton<IDocumentCategoryRegistry, DocumentCategoryRegistry>();
            services.AddSingleton<IBenchmarkMetric, CharacterCountMetric>();
            services.AddSingleton<IBenchmarkMetric, ContentCharacterCountMetric>();
            services.AddSingleton<IBenchmarkMetric, WordAndTokenCountMetric>();
            services.AddSingleton<IBenchmarkMetric, ExecutionLatencyMetric>();
            services.AddSingleton<IBenchmarkMetric, MemoryAllocationMetric>();
            services.AddSingleton<IBenchmarkMetric, TextCleanlinessMetric>();
            services.AddScoped<IBenchmarkRunnerService, BenchmarkRunnerService>();
            services.AddScoped<IVectorStoreService, VectorStoreService>();
            services.AddScoped<IKnowledgebaseService, KnowledgebaseService>();

            // ViewModels
            services.AddTransient<HomeViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<ChatViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<KnowledgebaseViewModel>();
            services.AddTransient<CreateKnowledgebaseViewModel>();
            services.AddTransient<ViewExtractedTextViewModel>();

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
            MainWindowInstance = _window;
            _window.Activate();
        }
    }
}

