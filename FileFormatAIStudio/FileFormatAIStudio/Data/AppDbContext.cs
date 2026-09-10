using System;
using System.IO;
using FileFormatAIStudio.Data.Entities;
using FileFormatAIStudio.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FileFormatAIStudio.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<ProviderConfigEntity> Providers => Set<ProviderConfigEntity>();
        public DbSet<ModelConfigEntity> Models => Set<ModelConfigEntity>();
        public DbSet<ChatSessionEntity> Sessions => Set<ChatSessionEntity>();
        public DbSet<ChatMessageEntity> Messages => Set<ChatMessageEntity>();
        public DbSet<AppSettingEntity> AppSettings => Set<AppSettingEntity>();
        public DbSet<KnowledgebaseEntity> Knowledgebases => Set<KnowledgebaseEntity>();
        public DbSet<KnowledgebaseDocumentEntity> KnowledgebaseDocuments => Set<KnowledgebaseDocumentEntity>();
        public DbSet<DocumentChunkEntity> DocumentChunks => Set<DocumentChunkEntity>();
        public DbSet<SessionKnowledgebaseEntity> SessionKnowledgebases => Set<SessionKnowledgebaseEntity>();

        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDir = Path.Combine(localAppData, "FileFormatAIStudio");
                if (!Directory.Exists(appDir))
                {
                    Directory.CreateDirectory(appDir);
                }

                string dbPath = Path.Combine(appDir, "fileformat_studio.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // DPAPI encryption for sensitive API keys at rest
            var dpapiConverter = new ValueConverter<string, string>(
                v => DataProtectionService.Protect(v),
                v => DataProtectionService.Unprotect(v));

            modelBuilder.Entity<ProviderConfigEntity>()
                .Property(p => p.ApiKey)
                .HasConversion(dpapiConverter);

            // Provider -> Models (Cascade Delete)
            modelBuilder.Entity<ProviderConfigEntity>()
                .HasMany(p => p.Models)
                .WithOne(m => m.Provider)
                .HasForeignKey(m => m.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Session -> Messages (Cascade Delete)
            modelBuilder.Entity<ChatSessionEntity>()
                .HasMany(s => s.Messages)
                .WithOne(m => m.Session)
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            // AppSettingEntity
            modelBuilder.Entity<AppSettingEntity>()
                .HasKey(s => s.Key);

            // Knowledgebase -> Documents (Cascade Delete)
            modelBuilder.Entity<KnowledgebaseEntity>()
                .HasMany(k => k.Documents)
                .WithOne(d => d.Knowledgebase)
                .HasForeignKey(d => d.KnowledgebaseId)
                .OnDelete(DeleteBehavior.Cascade);

            // Knowledgebase -> Chunks (Cascade Delete)
            modelBuilder.Entity<KnowledgebaseEntity>()
                .HasMany(k => k.Chunks)
                .WithOne(c => c.Knowledgebase)
                .HasForeignKey(c => c.KnowledgebaseId)
                .OnDelete(DeleteBehavior.Cascade);

            // KnowledgebaseDocument -> Chunks (Cascade Delete)
            modelBuilder.Entity<KnowledgebaseDocumentEntity>(entity =>
            {
                entity.HasMany(d => d.Chunks)
                    .WithOne(c => c.Document)
                    .HasForeignKey(c => c.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(d => d.KnowledgebaseId)
                    .HasDatabaseName("IX_KnowledgebaseDocuments_KnowledgebaseId");
            });

            // DocumentChunkEntity Configuration & Indexes
            modelBuilder.Entity<DocumentChunkEntity>(entity =>
            {
                entity.HasIndex(c => c.KnowledgebaseId)
                    .HasDatabaseName("IX_DocumentChunks_KnowledgebaseId");

                entity.HasIndex(c => c.DocumentId)
                    .HasDatabaseName("IX_DocumentChunks_DocumentId");

                entity.Property(c => c.EmbeddingVector)
                    .HasColumnType("BLOB");
            });

            // SessionKnowledgebaseEntity Join Table
            modelBuilder.Entity<SessionKnowledgebaseEntity>(entity =>
            {
                entity.HasKey(sk => new { sk.SessionId, sk.KnowledgebaseId });

                entity.HasOne(sk => sk.Session)
                    .WithMany(s => s.SessionKnowledgebases)
                    .HasForeignKey(sk => sk.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sk => sk.Knowledgebase)
                    .WithMany(k => k.SessionKnowledgebases)
                    .HasForeignKey(sk => sk.KnowledgebaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}

