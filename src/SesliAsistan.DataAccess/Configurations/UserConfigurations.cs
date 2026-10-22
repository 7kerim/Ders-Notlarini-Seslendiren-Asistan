using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliAsistan.Entities.Entities;

namespace SesliAsistan.DataAccess.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.FullName).HasMaxLength(120).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(100).IsRequired();
        b.Property(x => x.PreferredVoice).HasMaxLength(64);
        b.Property(x => x.PlaybackSpeed).HasPrecision(3, 2);
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Ignore(x => x.IsActive);
        b.HasOne(x => x.User).WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> b)
    {
        b.ToTable("friendships");
        b.HasIndex(x => new { x.RequesterId, x.AddresseeId }).IsUnique();
        b.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Addressee).WithMany().HasForeignKey(x => x.AddresseeId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
    }
}

public class StudyGroupConfiguration : IEntityTypeConfiguration<StudyGroup>
{
    public void Configure(EntityTypeBuilder<StudyGroup> b)
    {
        b.ToTable("study_groups");
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> b)
    {
        b.ToTable("group_members");
        b.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
        b.HasOne(x => x.Group).WithMany(g => g.Members).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SharedContentConfiguration : IEntityTypeConfiguration<SharedContent>
{
    public void Configure(EntityTypeBuilder<SharedContent> b)
    {
        b.ToTable("shared_contents");
        b.HasIndex(x => new { x.ContentType, x.ContentId });
        b.HasIndex(x => x.Privacy);
        b.Property(x => x.ContentType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Privacy).HasConversion<string>().HasMaxLength(20);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> b)
    {
        b.ToTable("content_reports");
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SharedContent).WithMany().HasForeignKey(x => x.SharedContentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.HasIndex(x => new { x.UserId, x.IsRead });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
