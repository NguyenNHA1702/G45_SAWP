using Core.Entities;

namespace Core.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameOrEmailWithRolesAsync(string usernameOrEmail, CancellationToken cancellationToken = default);
    Task<User?> GetByIdWithRolesAsync(int userId, CancellationToken cancellationToken = default);
}
