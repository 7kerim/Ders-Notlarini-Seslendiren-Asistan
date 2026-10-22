namespace SesliAsistan.Entities.Enums;

public enum UserRole { Student = 1, Admin = 2 }

public enum DocumentFileType { Pdf = 1, Docx = 2 }

/// <summary>Belge işleme hattının durumları (asenkron metin çıkarma).</summary>
public enum DocumentStatus { Uploaded = 0, Processing = 1, Ready = 2, Failed = 3 }

/// <summary>FG-7: Paylaşım gizlilik düzeyi.</summary>
public enum PrivacyLevel { Private = 0, Friends = 1, Group = 2, Public = 3 }

public enum FriendshipStatus { Pending = 0, Accepted = 1, Rejected = 2, Blocked = 3 }

/// <summary>FG-4: Özet uzunluğu.</summary>
public enum SummaryLength { Short = 1, Medium = 2, Detailed = 3 }

public enum SharedContentType { Summary = 1, FlashcardDeck = 2, Quiz = 3 }

public enum ReportStatus { Open = 0, Reviewed = 1, ContentRemoved = 2, Dismissed = 3 }

public enum GroupRole { Member = 0, Owner = 1 }
