using SesliAsistan.Entities.Common;
using SesliAsistan.Entities.Enums;

namespace SesliAsistan.Entities.Entities;

/// <summary>FG-3: Üretilmiş ses dosyası. ContentHash + Voice + Speed ile önbellek anahtarı oluşturulur.</summary>
public class AudioAsset : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public Guid? SectionId { get; set; }
    public Guid? SummaryId { get; set; }

    public string Voice { get; set; } = null!;
    public decimal Speed { get; set; } = 1.0m;
    public string ContentHash { get; set; } = null!;
    public string StorageKey { get; set; } = null!;
    public int DurationSeconds { get; set; }
}

/// <summary>FG-3: "Kaldığı yerden devam" için dinleme konumu.</summary>
public class ListeningProgress : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public Guid? SectionId { get; set; }
    public int PositionSeconds { get; set; }
}

/// <summary>FG-4: Sesli özet.</summary>
public class Summary : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public SummaryLength Length { get; set; }
    public string Content { get; set; } = null!;
    public PrivacyLevel Privacy { get; set; } = PrivacyLevel.Private;
}

/// <summary>FG-6: Bilgi kartı destesi.</summary>
public class FlashcardDeck : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public string Title { get; set; } = null!;
    public PrivacyLevel Privacy { get; set; } = PrivacyLevel.Private;
    public ICollection<Flashcard> Cards { get; set; } = new List<Flashcard>();
}

public class Flashcard : BaseEntity
{
    public Guid DeckId { get; set; }
    public FlashcardDeck Deck { get; set; } = null!;
    public string Front { get; set; } = null!;
    public string Back { get; set; } = null!;
    public int OrderIndex { get; set; }
}

/// <summary>FG-6: GenAI ile üretilen çoktan seçmeli test.</summary>
public class Quiz : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public string Title { get; set; } = null!;
    public PrivacyLevel Privacy { get; set; } = PrivacyLevel.Private;
    public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
}

public class QuizQuestion : BaseEntity
{
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public string QuestionText { get; set; } = null!;
    /// <summary>Seçenekler JSON dizi olarak saklanır (PostgreSQL jsonb).</summary>
    public string OptionsJson { get; set; } = "[]";
    public int CorrectOptionIndex { get; set; }
    public string? Explanation { get; set; }
    public int OrderIndex { get; set; }
}

/// <summary>FG-6: Başarı geçmişi.</summary>
public class QuizAttempt : BaseEntity
{
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
    public DateTime CompletedAt { get; set; }
}
