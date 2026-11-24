using Core.Enums;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class SessionValidator : ISessionValidator
{
    private readonly IUnitOfWork _unitOfWork;

    public SessionValidator(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SessionCheckResult> CheckAsync(int userId, int sessionVersion, CancellationToken ct = default)
    {
        var state = await _unitOfWork.Users.Query()
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Status, u.SessionVersion, u.MustChangePassword })
            .FirstOrDefaultAsync(ct);

        if (state == null || state.Status != AccountStatus.Active || state.SessionVersion != sessionVersion)
        {
            return new SessionCheckResult(false, false);
        }
        return new SessionCheckResult(true, state.MustChangePassword);
    }
}
