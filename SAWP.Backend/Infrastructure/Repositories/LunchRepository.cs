using Core.Entities;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class LunchRepository : Repository<LunchRegistration>, ILunchRepository
{
    public LunchRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<LunchRegistration>> GetDailyRegistrationsAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        return await _context.LunchRegistrations
            .Include(lr => lr.Student)
            .Where(lr => lr.Date.Date == date.Date && lr.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<LunchRegistration?> GetStudentRegistrationForDateAsync(int studentId, DateTime date, CancellationToken cancellationToken = default)
    {
        return await _context.LunchRegistrations
            .FirstOrDefaultAsync(lr => lr.StudentId == studentId && lr.Date == date, cancellationToken);
    }

    public async Task<Dictionary<int, LunchRegistration>> GetRegistrationsForStudentsAsync(
        IEnumerable<int> studentIds, 
        DateTime date, 
        bool track = false, 
        CancellationToken ct = default)
    {
        var idList = studentIds.ToList();
        IQueryable<LunchRegistration> query = _context.LunchRegistrations;
        if (!track) query = query.AsNoTracking();

        return await query
            .Where(r => idList.Contains(r.StudentId) && r.Date == date)
            .ToDictionaryAsync(r => r.StudentId, ct);
    }

    public async Task<IReadOnlyList<HeadcountRow>> GetHeadcountRowsAsync(DateTime date, CancellationToken ct = default)
    {
        var targetDate = date.Date;

        // Lấy danh sách đăng ký không nằm trong hàng chờ, gom duy nhất 1 dòng / học sinh
        var regs = await _context.LunchRegistrations
            .AsNoTracking()
            .Where(r => r.Date == targetDate && !r.IsWaitlisted)
            .ToListAsync(ct);

        if (regs.Count == 0)
        {
            return Array.Empty<HeadcountRow>();
        }

        var studentIds = regs.Select(r => r.StudentId).Distinct().ToList();

        var activeSchoolYearId = await _context.SchoolYears
            .AsNoTracking()
            .Where(sy => sy.IsActive)
            .OrderByDescending(sy => sy.Id)
            .Select(sy => (int?)sy.Id)
            .FirstOrDefaultAsync(ct);

        // Mỗi học sinh chỉ lấy duy nhất 1 bản ghi xếp lớp còn hiệu lực mới nhất thuộc năm học hiện tại (chống đếm trùng & lệch năm học)
        var assignments = await _context.ClassAssignments
            .AsNoTracking()
            .Include(ca => ca.Class)
            .Where(ca => studentIds.Contains(ca.StudentId)
                && ca.IsActive
                && ca.StartDate <= targetDate
                && (ca.EndDate == null || ca.EndDate >= targetDate)
                && ca.Class.Status == "Active"
                && (activeSchoolYearId == null || ca.Class.SchoolYearId == activeSchoolYearId.Value || ca.Class.SchoolYearId == 0))
            .ToListAsync(ct);

        var latestClassByStudent = assignments
            .GroupBy(ca => ca.StudentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(ca => ca.StartDate).ThenByDescending(ca => ca.Id).First());

        var rows = new List<HeadcountRow>(regs.Count);
        foreach (var group in regs.GroupBy(r => r.StudentId))
        {
            var r = group.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Id).First();
            if (latestClassByStudent.TryGetValue(r.StudentId, out var ca))
            {
                rows.Add(new HeadcountRow(r.StudentId, ca.ClassId, ca.Class?.ClassName ?? "Chưa phân lớp", r.MealType, r.IsActive, r.CancellationSource));
            }
            else
            {
                rows.Add(new HeadcountRow(r.StudentId, 0, "Chưa phân lớp", r.MealType, r.IsActive, r.CancellationSource));
            }
        }

        return rows;
    }
}

