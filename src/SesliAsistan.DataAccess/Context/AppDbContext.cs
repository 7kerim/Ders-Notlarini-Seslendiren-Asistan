using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SesliAsistan.Entities.Common;
using SesliAsistan.Entities.Entities;

namespace SesliAsistan.DataAccess.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentSection> DocumentSections => Set<DocumentSection>();
    public DbSet<AudioAsset> AudioAssets => Set<AudioAsset>();
    public DbSet<ListeningProgress> ListeningProgresses => Set<ListeningProgress>();
    public DbSet<Summary> Summaries => Set<Summary>();
    public DbSet<FlashcardDeck> FlashcardDecks => Set<FlashcardDeck>();
    public DbSet<Flashcard> Flashcards => Set<Flashcard>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<StudyGroup> StudyGroups => Set<StudyGroup>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<SharedContent> SharedContents => Set<SharedContent>();
    public DbSet<ContentReport> ContentReports => Set<ContentReport>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configurations/ klasöründeki tüm IEntityTypeConfiguration sınıflarını uygula
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Yumuşak silinen kayıtları tüm sorgulardan otomatik olarak gizleyen global filtre
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)) continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Equal(
                Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)),
                Expression.Constant(false));
            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = now;
            else if (entry.State == EntityState.Modified) entry.Entity.UpdatedAt = now;
        }

        // Fiziksel silme isteklerini yumuşak silmeye çevir
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted) continue;
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = now;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
