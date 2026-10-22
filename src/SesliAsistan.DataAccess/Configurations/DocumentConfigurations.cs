using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliAsistan.Entities.Entities;

namespace SesliAsistan.DataAccess.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("documents");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.StoragePath).HasMaxLength(500).IsRequired();
        b.Property(x => x.FileType).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ErrorMessage).HasMaxLength(1000);
        b.HasIndex(x => new { x.OwnerId, x.CreatedAt });
        b.HasOne(x => x.Owner).WithMany(u => u.Documents)
            .HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentSectionConfiguration : IEntityTypeConfiguration<DocumentSection>
{
    public void Configure(EntityTypeBuilder<DocumentSection> b)
    {
        b.ToTable("document_sections");
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.HasIndex(x => new { x.DocumentId, x.OrderIndex });
        b.HasOne(x => x.Document).WithMany(d => d.Sections)
            .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        // Konu ağacı: kendine referans veren ilişki
        b.HasOne(x => x.Parent).WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AudioAssetConfiguration : IEntityTypeConfiguration<AudioAsset>
{
    public void Configure(EntityTypeBuilder<AudioAsset> b)
    {
        b.ToTable("audio_assets");
        b.Property(x => x.Voice).HasMaxLength(64).IsRequired();
        b.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.Speed).HasPrecision(3, 2);
        // Aynı metin + ses + hız için tek bir ses dosyası üretilir (TTS maliyet optimizasyonu)
        b.HasIndex(x => new { x.ContentHash, x.Voice, x.Speed }).IsUnique();
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ListeningProgressConfiguration : IEntityTypeConfiguration<ListeningProgress>
{
    public void Configure(EntityTypeBuilder<ListeningProgress> b)
    {
        b.ToTable("listening_progress");
        b.HasIndex(x => new { x.UserId, x.DocumentId }).IsUnique();
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SummaryConfiguration : IEntityTypeConfiguration<Summary>
{
    public void Configure(EntityTypeBuilder<Summary> b)
    {
        b.ToTable("summaries");
        b.Property(x => x.Length).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Privacy).HasConversion<string>().HasMaxLength(20);
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FlashcardDeckConfiguration : IEntityTypeConfiguration<FlashcardDeck>
{
    public void Configure(EntityTypeBuilder<FlashcardDeck> b)
    {
        b.ToTable("flashcard_decks");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Privacy).HasConversion<string>().HasMaxLength(20);
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FlashcardConfiguration : IEntityTypeConfiguration<Flashcard>
{
    public void Configure(EntityTypeBuilder<Flashcard> b)
    {
        b.ToTable("flashcards");
        b.HasOne(x => x.Deck).WithMany(d => d.Cards).HasForeignKey(x => x.DeckId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> b)
    {
        b.ToTable("quizzes");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Privacy).HasConversion<string>().HasMaxLength(20);
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> b)
    {
        b.ToTable("quiz_questions");
        b.Property(x => x.OptionsJson).HasColumnType("jsonb");
        b.HasOne(x => x.Quiz).WithMany(q => q.Questions).HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> b)
    {
        b.ToTable("quiz_attempts");
        b.HasIndex(x => new { x.UserId, x.CompletedAt });
        b.HasOne(x => x.Quiz).WithMany().HasForeignKey(x => x.QuizId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
