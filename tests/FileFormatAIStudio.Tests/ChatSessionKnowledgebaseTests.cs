using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Data;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Chat;
using FileFormatAIStudio.Services.Knowledgebase;
using FileFormatAIStudio.Services.Settings;
using FileFormatAIStudio.ViewModels;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class ChatSessionKnowledgebaseTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public ChatSessionKnowledgebaseTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        [Fact]
        public async Task AttachKnowledgebaseAsync_PersistsLinkInDatabase()
        {
            var sessionId = Guid.NewGuid();
            var kbId = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                context.Sessions.Add(new ChatSessionEntity { Id = sessionId, Title = "Test Session" });
                context.Knowledgebases.Add(new KnowledgebaseEntity { Id = kbId, Name = "Research Docs" });
                await context.SaveChangesAsync();

                var service = new ChatSessionService(context);
                await service.AttachKnowledgebaseAsync(sessionId, kbId);
            }

            using (var verifyContext = new AppDbContext(_options))
            {
                var link = await verifyContext.SessionKnowledgebases
                    .FirstOrDefaultAsync(sk => sk.SessionId == sessionId && sk.KnowledgebaseId == kbId);

                link.Should().NotBeNull();
                link!.SessionId.Should().Be(sessionId);
                link.KnowledgebaseId.Should().Be(kbId);
            }
        }

        [Fact]
        public async Task AttachKnowledgebaseAsync_IsIdempotent()
        {
            var sessionId = Guid.NewGuid();
            var kbId = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                context.Sessions.Add(new ChatSessionEntity { Id = sessionId, Title = "Test Session" });
                context.Knowledgebases.Add(new KnowledgebaseEntity { Id = kbId, Name = "Research Docs" });
                await context.SaveChangesAsync();

                var service = new ChatSessionService(context);
                await service.AttachKnowledgebaseAsync(sessionId, kbId);
                // Call second time
                await service.AttachKnowledgebaseAsync(sessionId, kbId);
            }

            using (var verifyContext = new AppDbContext(_options))
            {
                var count = await verifyContext.SessionKnowledgebases
                    .CountAsync(sk => sk.SessionId == sessionId && sk.KnowledgebaseId == kbId);

                count.Should().Be(1);
            }
        }

        [Fact]
        public async Task DetachKnowledgebaseAsync_RemovesLinkFromDatabase()
        {
            var sessionId = Guid.NewGuid();
            var kbId = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                context.Sessions.Add(new ChatSessionEntity { Id = sessionId, Title = "Test Session" });
                context.Knowledgebases.Add(new KnowledgebaseEntity { Id = kbId, Name = "Research Docs" });
                context.SessionKnowledgebases.Add(new SessionKnowledgebaseEntity
                {
                    SessionId = sessionId,
                    KnowledgebaseId = kbId
                });
                await context.SaveChangesAsync();

                var service = new ChatSessionService(context);
                await service.DetachKnowledgebaseAsync(sessionId, kbId);
            }

            using (var verifyContext = new AppDbContext(_options))
            {
                var exists = await verifyContext.SessionKnowledgebases
                    .AnyAsync(sk => sk.SessionId == sessionId && sk.KnowledgebaseId == kbId);

                exists.Should().BeFalse();
            }
        }

        [Fact]
        public async Task GetAttachedKnowledgebasesAsync_ReturnsAttachedEntitiesOrderedByName()
        {
            var sessionId = Guid.NewGuid();
            var kb1Id = Guid.NewGuid();
            var kb2Id = Guid.NewGuid();
            var kbUnattachedId = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                context.Sessions.Add(new ChatSessionEntity { Id = sessionId, Title = "Test Session" });
                context.Knowledgebases.AddRange(
                    new KnowledgebaseEntity { Id = kb1Id, Name = "Zeta KB" },
                    new KnowledgebaseEntity { Id = kb2Id, Name = "Alpha KB" },
                    new KnowledgebaseEntity { Id = kbUnattachedId, Name = "Unattached KB" }
                );
                context.SessionKnowledgebases.AddRange(
                    new SessionKnowledgebaseEntity { SessionId = sessionId, KnowledgebaseId = kb1Id },
                    new SessionKnowledgebaseEntity { SessionId = sessionId, KnowledgebaseId = kb2Id }
                );
                await context.SaveChangesAsync();

                var service = new ChatSessionService(context);
                var attached = await service.GetAttachedKnowledgebasesAsync(sessionId);

                attached.Should().HaveCount(2);
                attached[0].Name.Should().Be("Alpha KB");
                attached[1].Name.Should().Be("Zeta KB");
            }
        }

        [Fact]
        public async Task GetSessionAsync_EagerlyIncludesSessionKnowledgebases()
        {
            var sessionId = Guid.NewGuid();
            var kbId = Guid.NewGuid();

            using (var context = new AppDbContext(_options))
            {
                context.Sessions.Add(new ChatSessionEntity { Id = sessionId, Title = "Test Session" });
                context.Knowledgebases.Add(new KnowledgebaseEntity { Id = kbId, Name = "Finance Docs" });
                context.SessionKnowledgebases.Add(new SessionKnowledgebaseEntity
                {
                    SessionId = sessionId,
                    KnowledgebaseId = kbId
                });
                await context.SaveChangesAsync();

                var service = new ChatSessionService(context);
                var loaded = await service.GetSessionAsync(sessionId);

                loaded.Should().NotBeNull();
                loaded!.SessionKnowledgebases.Should().HaveCount(1);
                loaded.SessionKnowledgebases[0].Knowledgebase.Should().NotBeNull();
                loaded.SessionKnowledgebases[0].Knowledgebase.Name.Should().Be("Finance Docs");
            }
        }

        [Fact]
        public async Task ChatViewModel_AttachAndDetachCommands_UpdateStateAndCollections()
        {
            var sessionId = Guid.NewGuid();
            var kb1 = new KnowledgebaseEntity { Id = Guid.NewGuid(), Name = "KB One" };
            var kb2 = new KnowledgebaseEntity { Id = Guid.NewGuid(), Name = "KB Two" };

            var fakeSessionService = new FakeChatSessionService();
            var fakeKbService = new FakeKnowledgebaseService();
            var fakeSettingsService = new FakeSettingsService();
            var fakeExecService = new FakeChatExecutionService();

            var session = new ChatSessionEntity { Id = sessionId, Title = "Chat Session" };
            fakeSessionService.Sessions.Add(session);
            fakeKbService.Knowledgebases.AddRange([kb1, kb2]);

            var vm = new ChatViewModel(fakeSessionService, fakeExecService, fakeSettingsService, fakeKbService);
            await vm.InitializeAsync(sessionId);

            // Initially no attached KBs, 2 available
            vm.AttachedKnowledgebases.Should().BeEmpty();
            vm.HasAttachedKnowledgebases.Should().BeFalse();
            vm.AvailableKnowledgebasesToAttach.Should().HaveCount(2);
            vm.HasAvailableKnowledgebases.Should().BeTrue();

            // Attach KB 1
            await vm.AttachKnowledgebaseCommand.ExecuteAsync(kb1);

            vm.AttachedKnowledgebases.Should().HaveCount(1);
            vm.AttachedKnowledgebases[0].Id.Should().Be(kb1.Id);
            vm.HasAttachedKnowledgebases.Should().BeTrue();
            vm.AttachedKnowledgebasesCountText.Should().Be("1 knowledgebase attached");
            vm.AvailableKnowledgebasesToAttach.Should().HaveCount(1);
            vm.AvailableKnowledgebasesToAttach[0].Id.Should().Be(kb2.Id);

            // Verify underlying session service was called
            var attachedInService = await fakeSessionService.GetAttachedKnowledgebasesAsync(sessionId);
            attachedInService.Should().ContainSingle(x => x.Id == kb1.Id);

            // Detach KB 1
            await vm.DetachKnowledgebaseCommand.ExecuteAsync(kb1);

            vm.AttachedKnowledgebases.Should().BeEmpty();
            vm.HasAttachedKnowledgebases.Should().BeFalse();
            vm.AvailableKnowledgebasesToAttach.Should().HaveCount(2);
            vm.HasAvailableKnowledgebases.Should().BeTrue();

            var attachedAfterDetach = await fakeSessionService.GetAttachedKnowledgebasesAsync(sessionId);
            attachedAfterDetach.Should().BeEmpty();
        }

        private class FakeChatSessionService : IChatSessionService
        {
            public List<ChatSessionEntity> Sessions { get; } = new();
            private readonly List<SessionKnowledgebaseEntity> _links = new();

            public Task<List<ChatSessionEntity>> GetSessionsAsync() => Task.FromResult(Sessions);

            public Task<ChatSessionEntity?> GetEmptySessionAsync() =>
                Task.FromResult(Sessions.FirstOrDefault(s => s.Messages.Count == 0));

            public Task<ChatSessionEntity> CreateSessionAsync(string title = "New Chat", Guid? defaultModelId = null)
            {
                var s = new ChatSessionEntity { Id = Guid.NewGuid(), Title = title, SelectedModelId = defaultModelId };
                Sessions.Add(s);
                return Task.FromResult(s);
            }

            public Task<ChatSessionEntity?> GetSessionAsync(Guid sessionId) =>
                Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));

            public Task UpdateSessionTitleAsync(Guid sessionId, string title)
            {
                var s = Sessions.FirstOrDefault(x => x.Id == sessionId);
                if (s != null) s.Title = title;
                return Task.CompletedTask;
            }

            public Task UpdateSessionModelAsync(Guid sessionId, Guid modelId)
            {
                var s = Sessions.FirstOrDefault(x => x.Id == sessionId);
                if (s != null) s.SelectedModelId = modelId;
                return Task.CompletedTask;
            }

            public Task DeleteSessionAsync(Guid sessionId)
            {
                Sessions.RemoveAll(x => x.Id == sessionId);
                _links.RemoveAll(x => x.SessionId == sessionId);
                return Task.CompletedTask;
            }

            public Task<ChatMessageEntity> AddMessageAsync(Guid sessionId, string role, string content)
            {
                var msg = new ChatMessageEntity { SessionId = sessionId, Role = role, Content = content };
                var s = Sessions.FirstOrDefault(x => x.Id == sessionId);
                s?.Messages.Add(msg);
                return Task.FromResult(msg);
            }

            public Task<List<KnowledgebaseEntity>> GetAttachedKnowledgebasesAsync(Guid sessionId)
            {
                var kbs = _links.Where(x => x.SessionId == sessionId && x.Knowledgebase != null).Select(x => x.Knowledgebase).ToList();
                return Task.FromResult(kbs);
            }

            public Task AttachKnowledgebaseAsync(Guid sessionId, Guid knowledgebaseId)
            {
                if (!_links.Any(x => x.SessionId == sessionId && x.KnowledgebaseId == knowledgebaseId))
                {
                    _links.Add(new SessionKnowledgebaseEntity
                    {
                        SessionId = sessionId,
                        KnowledgebaseId = knowledgebaseId,
                        Knowledgebase = new KnowledgebaseEntity { Id = knowledgebaseId, Name = "Mock KB" }
                    });
                }
                return Task.CompletedTask;
            }

            public Task DetachKnowledgebaseAsync(Guid sessionId, Guid knowledgebaseId)
            {
                _links.RemoveAll(x => x.SessionId == sessionId && x.KnowledgebaseId == knowledgebaseId);
                return Task.CompletedTask;
            }
        }

        private class FakeSettingsService : ISettingsService
        {
            public Task<List<ProviderConfigEntity>> GetProvidersAsync() => Task.FromResult(new List<ProviderConfigEntity>());
            public Task<ProviderConfigEntity?> GetProviderByIdAsync(Guid providerId) => Task.FromResult<ProviderConfigEntity?>(null);
            public Task SaveProviderAsync(ProviderConfigEntity provider) => Task.CompletedTask;
            public Task DeleteProviderAsync(Guid providerId) => Task.CompletedTask;
            public Task AddModelAsync(ModelConfigEntity model) => Task.CompletedTask;
            public Task DeleteModelAsync(Guid modelId) => Task.CompletedTask;
            public Task<List<ModelConfigEntity>> GetAllEnabledModelsAsync() => Task.FromResult(new List<ModelConfigEntity>());
            public Task RestoreDefaultProvidersAsync() => Task.CompletedTask;
        }

        private class FakeChatExecutionService : IChatExecutionService
        {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            public async IAsyncEnumerable<ChatResponseUpdate> StreamResponseAsync(
                ProviderConfigEntity provider,
                string modelId,
                IEnumerable<ChatMessage> history,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
            {
                yield break;
            }
#pragma warning restore CS1998
        }
    }
}

