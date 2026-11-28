using Core.Entities;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByUsernameOrEmailWithRolesAsync(string usernameOrEmail, CancellationToken cancellationToken = default)
    {
        var normalized = usernameOrEmail.Trim().ToLower();
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.StaffProfile)
            .Include(u => u.Guardian)
            .Where(u => u.Email.ToLower() == normalized)
            .OrderBy(u => u.Status == Core.Enums.AccountStatus.Deactivated ? 1 : 0)
            .ThenByDescending(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User?> GetByIdWithRolesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.StaffProfile)
            .Include(u => u.Guardian)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }
}
