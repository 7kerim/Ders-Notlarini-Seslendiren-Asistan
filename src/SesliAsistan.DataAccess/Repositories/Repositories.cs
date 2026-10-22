using Microsoft.EntityFrameworkCore;
using SesliAsistan.DataAccess.Context;
using SesliAsistan.Entities.Common;
using SesliAsistan.Entities.Entities;

namespace SesliAsistan.DataAccess.Repositories;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    IQueryable<T> Query();
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> Set;

    public GenericRepository(AppDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(x => x.Id == id, ct);

    public IQueryable<T> Query() => Set.AsQueryable();

    public async Task AddAsync(T entity, CancellationToken ct = default) => await Set.AddAsync(entity, ct);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);
}

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
}

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) =>
        Set.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct);
}

public interface IRefreshTokenRepository : IGenericRepository<RefreshToken>
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
}

public class RefreshTokenRepository : GenericRepository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(AppDbContext context) : base(context) { }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        Set.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
}

public interface IDocumentRepository : IGenericRepository<Document>
{
    Task<(IReadOnlyList<Document> Items, int Total)> GetPagedByOwnerAsync(
        Guid ownerId, int page, int pageSize, string? search, CancellationToken ct = default);
    Task<Document?> GetWithSectionsAsync(Guid id, CancellationToken ct = default);
}

public class DocumentRepository : GenericRepository<Document>, IDocumentRepository
{
    public DocumentRepository(AppDbContext context) : base(context) { }

    public async Task<(IReadOnlyList<Document> Items, int Total)> GetPagedByOwnerAsync(
        Guid ownerId, int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().Where(d => d.OwnerId == ownerId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(d => d.Title.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<Document?> GetWithSectionsAsync(Guid id, CancellationToken ct = default) =>
        Set.AsNoTracking()
           .Include(d => d.Sections.OrderBy(s => s.OrderIndex))
           .FirstOrDefaultAsync(d => d.Id == id, ct);
}

/// <summary>Birden fazla repository üzerindeki değişiklikleri tek işlemde kaydeder.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    public UnitOfWork(AppDbContext context) => _context = context;
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
