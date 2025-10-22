using Decatron.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Decatron.Data
{
    public class DecatronDbContext : DbContext
    {
        public DecatronDbContext(DbContextOptions<DecatronDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<SystemSettings> SystemSettings { get; set; }
        public DbSet<UserAccess> UserAccess { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<BotTokens> BotTokens { get; set; }
        public DbSet<CommandSettings> CommandSettings { get; set; }
        public DbSet<TitleHistory> TitleHistory { get; set; }
        public DbSet<GameHistory> GameHistory { get; set; }
        public DbSet<MicroGameCommands> MicroGameCommands { get; set; }
        public DbSet<Categories> Categories { get; set; }
        public DbSet<UserChannelPermissions> UserChannelPermissions { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Users Configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TwitchId).IsRequired().HasMaxLength(50).HasColumnName("TwitchId");
                entity.Property(e => e.Login).IsRequired().HasMaxLength(100).HasColumnName("Login");
                entity.Property(e => e.DisplayName).HasMaxLength(150).HasColumnName("DisplayName");
                entity.Property(e => e.Email).HasMaxLength(255).HasColumnName("Email");
                entity.Property(e => e.ProfileImageUrl).HasMaxLength(500).HasColumnName("ProfileImageUrl");
                entity.Property(e => e.OfflineImageUrl).HasMaxLength(500).HasColumnName("OfflineImageUrl");
                entity.Property(e => e.BroadcasterType).HasMaxLength(50).HasColumnName("BroadcasterType");
                entity.Property(e => e.ViewCount).HasColumnName("ViewCount");
                entity.Property(e => e.Description).HasMaxLength(500).HasColumnName("Description");
                entity.Property(e => e.AccessToken).IsRequired().HasMaxLength(500).HasColumnName("AccessToken");
                entity.Property(e => e.RefreshToken).HasMaxLength(500).HasColumnName("RefreshToken");
                entity.Property(e => e.TokenExpiration).IsRequired().HasColumnName("TokenExpiration");
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnName("CreatedAt");
                entity.Property(e => e.UpdatedAt).IsRequired().HasColumnName("UpdatedAt");
                entity.Property(e => e.IsActive).IsRequired().HasColumnName("IsActive");
                entity.Property(e => e.UniqueId).HasMaxLength(50).HasColumnName("unique_id");

                entity.HasIndex(e => e.TwitchId).IsUnique();
                entity.HasIndex(e => e.Login).IsUnique();
                // REMOVIDO: entity.HasIndex(e => e.Email).IsUnique(); - Permite emails duplicados
                entity.HasIndex(e => e.Email); // Solo índice para optimizar búsquedas, no único
                entity.HasIndex(e => e.UniqueId).IsUnique();

                entity.ToTable("users");
            });

            // SystemSettings Configuration
            modelBuilder.Entity<SystemSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasColumnName("UserId");
                entity.Property(e => e.BotEnabled).IsRequired().HasDefaultValue(true).HasColumnName("BotEnabled");
                entity.Property(e => e.CommandsEnabled).IsRequired().HasDefaultValue(true).HasColumnName("CommandsEnabled");
                entity.Property(e => e.CommandCooldown).IsRequired().HasDefaultValue(5).HasColumnName("CommandCooldown");
                entity.Property(e => e.TimersEnabled).IsRequired().HasDefaultValue(true).HasColumnName("TimersEnabled");
                entity.Property(e => e.TimerMinMessages).IsRequired().HasDefaultValue(5).HasColumnName("TimerMinMessages");
                entity.Property(e => e.AutoModerationEnabled).IsRequired().HasDefaultValue(true).HasColumnName("AutoModerationEnabled");
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnName("CreatedAt");
                entity.Property(e => e.UpdatedAt).IsRequired().HasColumnName("UpdatedAt");

                entity.HasIndex(e => e.UserId).IsUnique();
                entity.ToTable("system_settings");
            });

            // UserAccess Configuration
            modelBuilder.Entity<UserAccess>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasColumnName("UserId");
                entity.Property(e => e.AuthorizedUserId).IsRequired().HasColumnName("AuthorizedUserId");
                entity.Property(e => e.PermissionLevel).IsRequired().HasMaxLength(50).HasColumnName("PermissionLevel");
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnName("CreatedAt");

                entity.HasIndex(e => e.UserId);
                entity.ToTable("user_access");
            });

            // ChatMessages Configuration
            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Channel).IsRequired().HasMaxLength(100).HasColumnName("Channel");
                entity.Property(e => e.Username).IsRequired().HasMaxLength(100).HasColumnName("Username");
                entity.Property(e => e.UserId).HasMaxLength(50).HasColumnName("UserId");
                entity.Property(e => e.Message).IsRequired().HasColumnName("Message");
                entity.Property(e => e.Timestamp).IsRequired().HasColumnName("Timestamp");
                entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");

                entity.HasIndex(e => new { e.Channel, e.Timestamp });
                entity.HasIndex(e => e.Username);
                entity.ToTable("chat_messages");
            });

            modelBuilder.Entity<BotTokens>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BotUsername).IsRequired().HasMaxLength(100).HasColumnName("BotUsername");
                entity.Property(e => e.AccessToken).IsRequired().HasMaxLength(500).HasColumnName("AccessToken");
                entity.Property(e => e.ChatToken).IsRequired().HasMaxLength(500).HasColumnName("ChatToken");
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnName("CreatedAt");
                entity.Property(e => e.UpdatedAt).IsRequired().HasColumnName("UpdatedAt");
                entity.Property(e => e.IsActive).IsRequired().HasColumnName("IsActive");

                entity.HasIndex(e => e.BotUsername).IsUnique();
                entity.ToTable("bot_tokens");
            });

            // CommandSettings Configuration
            modelBuilder.Entity<CommandSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasColumnName("UserId");
                entity.Property(e => e.CommandName).IsRequired().HasMaxLength(100).HasColumnName("CommandName");
                entity.Property(e => e.IsEnabled).IsRequired().HasDefaultValue(true).HasColumnName("IsEnabled");
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnName("CreatedAt");
                entity.Property(e => e.UpdatedAt).IsRequired().HasColumnName("UpdatedAt");

                entity.HasIndex(e => new { e.UserId, e.CommandName }).IsUnique();
                entity.HasIndex(e => e.CommandName);
                entity.ToTable("command_settings");

                // Foreign key relationship
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // TitleHistory Configuration
            modelBuilder.Entity<TitleHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChannelLogin).IsRequired().HasMaxLength(100).HasColumnName("channel_login");
                entity.Property(e => e.Title).IsRequired().HasMaxLength(500).HasColumnName("title");
                entity.Property(e => e.ChangedBy).IsRequired().HasMaxLength(100).HasColumnName("changed_by");
                entity.Property(e => e.ChangedAt).IsRequired().HasColumnName("changed_at");

                entity.HasIndex(e => e.ChannelLogin).HasDatabaseName("idx_channel");
                entity.HasIndex(e => e.ChangedAt).HasDatabaseName("idx_date");
                entity.ToTable("title_history");
            });

            // GameHistory Configuration
            modelBuilder.Entity<GameHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChannelLogin).IsRequired().HasMaxLength(100).HasColumnName("channel_login");
                entity.Property(e => e.CategoryName).IsRequired().HasMaxLength(500).HasColumnName("category_name");
                entity.Property(e => e.ChangedBy).IsRequired().HasMaxLength(100).HasColumnName("changed_by");
                entity.Property(e => e.ChangedAt).IsRequired().HasColumnName("changed_at");

                entity.HasIndex(e => e.ChannelLogin).HasDatabaseName("idx_channel");
                entity.HasIndex(e => e.ChangedAt).HasDatabaseName("idx_date");
                entity.ToTable("game_history");
            });

            // MicroGameCommands Configuration
            modelBuilder.Entity<MicroGameCommands>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ChannelName).IsRequired().HasMaxLength(100).HasColumnName("channel_name");
                entity.Property(e => e.ShortCommand).IsRequired().HasMaxLength(100).HasColumnName("short_command");
                entity.Property(e => e.CategoryName).IsRequired().HasMaxLength(500).HasColumnName("category_name");
                entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100).HasColumnName("created_by");
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).IsRequired().HasColumnName("updated_at");

                entity.HasIndex(e => new { e.ChannelName, e.ShortCommand }).IsUnique().HasDatabaseName("idx_channel_command");
                entity.HasIndex(e => e.ChannelName).HasDatabaseName("idx_channel");
                entity.ToTable("micro_game_commands");
            });

            // Categories Configuration
            modelBuilder.Entity<Categories>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255).HasColumnName("name");
                entity.Property(e => e.Priority).IsRequired().HasDefaultValue(0).HasColumnName("priority");
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).IsRequired().HasColumnName("updated_at");

                entity.HasIndex(e => e.Name).IsUnique().HasDatabaseName("idx_name");
                entity.HasIndex(e => e.Priority).HasDatabaseName("idx_priority");
                entity.ToTable("categories");
            });

            modelBuilder.Entity<UserChannelPermissions>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ChannelOwnerId).IsRequired().HasColumnName("channel_owner_id");
                entity.Property(e => e.GrantedUserId).IsRequired().HasColumnName("granted_user_id");
                entity.Property(e => e.AccessLevel).IsRequired().HasMaxLength(50).HasColumnName("access_level");
                entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true).HasColumnName("is_active");
                entity.Property(e => e.GrantedBy).IsRequired().HasColumnName("granted_by");
                entity.Property(e => e.CreatedAt).IsRequired().HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).IsRequired().HasColumnName("updated_at");

                // Relaciones
                entity.HasOne(e => e.ChannelOwner)
                    .WithMany()
                    .HasForeignKey(e => e.ChannelOwnerId)
                    .HasConstraintName("FK_UserChannelPermissions_ChannelOwner")
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.GrantedUser)
                    .WithMany()
                    .HasForeignKey(e => e.GrantedUserId)
                    .HasConstraintName("FK_UserChannelPermissions_GrantedUser")
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.GrantedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.GrantedBy)
                    .HasConstraintName("FK_UserChannelPermissions_GrantedBy")
                    .OnDelete(DeleteBehavior.Restrict);

                // Índices
                entity.HasIndex(e => new { e.ChannelOwnerId, e.GrantedUserId })
                    .IsUnique()
                    .HasDatabaseName("idx_channel_granted_user");

                entity.HasIndex(e => e.ChannelOwnerId).HasDatabaseName("idx_channel_owner");
                entity.HasIndex(e => e.GrantedUserId).HasDatabaseName("idx_granted_user");
                entity.HasIndex(e => e.AccessLevel).HasDatabaseName("idx_access_level");

                entity.ToTable("user_channel_permissions");
            });
        }
    }
}