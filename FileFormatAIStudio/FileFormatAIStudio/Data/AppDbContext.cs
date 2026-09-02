using System;
using System.IO;
using FileFormatAIStudio.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FileFormatAIStudio.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<ProviderConfigEntity> Providers => Set<ProviderConfigEntity>();
        public DbSet<ModelConfigEntity> Models => Set<ModelConfigEntity>();
        public DbSet<ChatSessionEntity> Sessions => Set<ChatSessionEntity>();
        public DbSet<ChatMessageEntity> Messages => Set<ChatMessageEntity>();

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
        }
    }
}

