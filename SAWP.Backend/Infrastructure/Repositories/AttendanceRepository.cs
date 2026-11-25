using Core.Common;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class AttendanceRepository : Repository<AttendanceRecord>, IAttendanceRepository
{
    public AttendanceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetDailyClassAttendanceAsync(
        int classId, 
        DateTime date, 
        AttendanceSession session,
        bool track = false, 
        CancellationToken cancellationToken = default)
    {
        IQueryable<AttendanceRecord> query = _context.AttendanceRecords;

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query
            .Include(ar => ar.Student)
            .Where(ar => ar.ClassId == classId && ar.Date == date && ar.Session == session)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetDailyStudentAttendanceAsync(
        IEnumerable<int> studentIds,
        DateTime date,
        AttendanceSession session,
        bool track = false,
        CancellationToken cancellationToken = default)
    {
        var ids = studentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Array.Empty<AttendanceRecord>();
        }

        IQueryable<AttendanceRecord> query = _context.AttendanceRecords;
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query
            .Include(ar => ar.Student)
            .Where(ar => ids.Contains(ar.StudentId) && ar.Date == date && ar.Session == session)
            .ToListAsync(cancellationToken);
    }

    public async Task<AttendanceRecord?> GetByIdWithStudentClassAsync(int id, CancellationToken ct = default)
    {
        return await _context.AttendanceRecords
            .Include(ar => ar.Student)
            .Include(ar => ar.Class)
            .FirstOrDefaultAsync(ar => ar.Id == id, ct);
    }

    public async Task<Dictionary<int, LeaveIndicatorDto>> GetActiveLeavesCoveringDateAsync(
        IEnumerable<int> studentIds, 
        DateTime date, 
        AttendanceSession session,
        CancellationToken ct = default)
    {
        var studentIdList = studentIds.ToList();
        // Đơn nghỉ phủ buổi này: nghỉ cả ngày hoặc đúng buổi Sáng/Chiều
        var matchingSession = session == AttendanceSession.Morning ? LeaveSession.Morning : LeaveSession.Afternoon;

        var leaves = await _context.LeaveRequests
            .AsNoTracking()
            .Where(lr => lr.AdministrativeRequest.StudentId != null
                      && studentIdList.Contains(lr.AdministrativeRequest.StudentId.Value)
                      && lr.AdministrativeRequest.RequestType == RequestTypes.Leave
                      && (lr.AdministrativeRequest.Status == RequestStatus.Submitted
                          || lr.AdministrativeRequest.Status == RequestStatus.UnderReview
                          || lr.AdministrativeRequest.Status == RequestStatus.Approved)
                      && lr.StartDate <= date && lr.EndDate >= date
                      && (lr.Session == LeaveSession.FullDay || lr.Session == matchingSession))
            .Select(lr => new 
            {
                StudentId = lr.AdministrativeRequest.StudentId!.Value,
                SubmissionDate = lr.AdministrativeRequest.SubmissionDate,
                Dto = new LeaveIndicatorDto
                {
                    LeaveRequestId = lr.Id,
                    RequestNumber  = lr.AdministrativeRequest.RequestNumber,
                    Status         = lr.AdministrativeRequest.Status,
                    StartDate      = lr.StartDate,
                    EndDate        = lr.EndDate,
                    Session        = lr.Session,
                    Reason         = lr.Reason
                }
            })
            .ToListAsync(ct);

        return leaves
            .GroupBy(x => x.StudentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Dto.Status == RequestStatus.Approved)
                      .ThenByDescending(x => x.SubmissionDate)
                      .First().Dto
            );
    }

    public async Task<IReadOnlyList<StudentAttendanceRowDto>> GetClassStatusCountsAsync(
        int classId, 
        DateTime from, 
        DateTime to, 
        CancellationToken ct = default)
    {
        var fromDate = from.Date;
        var toDate = to.Date;

        var classSchoolYearId = await _context.Classes
            .AsNoTracking()
            .Where(c => c.Id == classId)
            .Select(c => (int?)c.SchoolYearId)
            .FirstOrDefaultAsync(ct);

        var calendarQuery = _context.SchoolCalendarDays
            .AsNoTracking()
            .Where(d => d.Date >= fromDate && d.Date <= toDate);

        if (classSchoolYearId.HasValue && classSchoolYearId.Value > 0)
        {
            calendarQuery = calendarQuery.Where(d => d.SchoolYearId == classSchoolYearId.Value);
        }
        else
        {
            calendarQuery = calendarQuery.Where(d => d.SchoolYear.IsActive);
        }

        var calendarMap = (await calendarQuery.ToListAsync(ct))
            .GroupBy(d => d.Date.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().IsSchoolDay);

        bool IsValidSchoolDay(DateTime dt)
        {
            var d = dt.Date;
            if (calendarMap.TryGetValue(d, out var isSchoolDay)) return isSchoolDay;
            return d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday;
        }

        var activeRoster = await _context.ClassAssignments
            .AsNoTracking()
            .Where(ca => ca.ClassId == classId && ca.IsActive)
            .Select(ca => new
            {
                ca.StudentId,
                ca.Student.StudentCode,
                ca.Student.FullName,
                ca.StartDate,
                ca.EndDate
            })
            .ToListAsync(ct);

        if (activeRoster.Count > 0)
        {
            var rosterIds = activeRoster.Select(s => s.StudentId).ToList();
            var rosterRecords = (await _context.AttendanceRecords
                .AsNoTracking()
                .Where(ar => rosterIds.Contains(ar.StudentId) && ar.Date >= fromDate && ar.Date <= toDate)
                .Select(ar => new
                {
                    ar.Date,
                    ar.Session,
                    ar.StudentId,
                    ar.Status
                })
                .ToListAsync(ct))
                .Where(x => IsValidSchoolDay(x.Date))
                .GroupBy(x => x.StudentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var recordedSlots = (await _context.AttendanceRecords
                .AsNoTracking()
                .Where(ar => ar.ClassId == classId && ar.Date >= fromDate && ar.Date <= toDate)
                .Select(ar => new { ar.Date, ar.Session })
                .Distinct()
                .ToListAsync(ct))
                .Where(x => IsValidSchoolDay(x.Date))
                .Select(x => (Date: x.Date.Date, x.Session))
                .Distinct()
                .ToList();

            return activeRoster
                .Select(s =>
                {
                    var startDate = s.StartDate.Date;
                    var endDate = s.EndDate?.Date;
                    var studentRecords = rosterRecords.TryGetValue(s.StudentId, out var list)
                        ? list.Where(x => x.Date.Date >= startDate && (!endDate.HasValue || x.Date.Date <= endDate.Value)).ToList()
                        : null;

                    var present = studentRecords?.Count(x => x.Status == AttendanceStatus.Present) ?? 0;
                    var late = studentRecords?.Count(x => x.Status == AttendanceStatus.Late) ?? 0;
                    var excused = studentRecords?.Count(x => x.Status == AttendanceStatus.ExcusedAbsence) ?? 0;
                    var unexcused = studentRecords?.Count(x => x.Status == AttendanceStatus.UnexcusedAbsence) ?? 0;
                    var expectedSlots = recordedSlots.Count(slot => slot.Date >= startDate && (!endDate.HasValue || slot.Date <= endDate.Value));
                    var notMarked = Math.Max(0, expectedSlots - (present + late + excused + unexcused));

                    return new StudentAttendanceRowDto
                    {
                        StudentId = s.StudentId,
                        StudentCode = s.StudentCode,
                        FullName = s.FullName,
                        Present = present,
                        Late = late,
                        ExcusedAbsence = excused,
                        UnexcusedAbsence = unexcused,
                        NotMarked = notMarked
                    };
                })
                .OrderBy(x => x.FullName)
                .ToList();
        }

        var records = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(ar => ar.ClassId == classId && ar.Date >= fromDate && ar.Date <= toDate)
            .Select(ar => new 
            {
                ar.Date,
                ar.StudentId,
                ar.Student.StudentCode,
                ar.Student.FullName,
                ar.Status
            })
            .ToListAsync(ct);

        return records
            .Where(x => IsValidSchoolDay(x.Date))
            .GroupBy(x => new { x.StudentId, x.StudentCode, x.FullName })
            .Select(g => new StudentAttendanceRowDto
            {
                StudentId = g.Key.StudentId,
                StudentCode = g.Key.StudentCode,
                FullName = g.Key.FullName,
                Present = g.Count(x => x.Status == AttendanceStatus.Present),
                Late = g.Count(x => x.Status == AttendanceStatus.Late),
                ExcusedAbsence = g.Count(x => x.Status == AttendanceStatus.ExcusedAbsence),
                UnexcusedAbsence = g.Count(x => x.Status == AttendanceStatus.UnexcusedAbsence)
            })
            .OrderBy(x => x.FullName)
            .ToList();
    }

    public async Task<HashSet<int>> GetSubmittedClassIdsAsync(
        IEnumerable<int> classIds, 
        DateTime date, 
        AttendanceSession session,
        CancellationToken ct = default)
    {
        var classIdList = classIds.ToList();
        if (classIdList.Count == 0)
        {
            return new HashSet<int>();
        }

        var activeCounts = await _context.ClassAssignments
            .AsNoTracking()
            .Where(ca => classIdList.Contains(ca.ClassId) && ca.IsActive)
            .GroupBy(ca => ca.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count, ct);

        // Bản ghi Vắng có phép tự sinh khi duyệt đơn (LeaveRequestId != null && UpdatedByUserId == null)
        // KHÔNG tính là giáo viên đã nộp điểm danh lớp (A3).
        // Chỉ đánh dấu "Đã lưu" khi toàn bộ học sinh hiện tại của lớp đều đã được điểm danh.
        var submittedCounts = await _context.AttendanceRecords
            .AsNoTracking()
            .Where(ar => classIdList.Contains(ar.ClassId)
                      && ar.Date == date
                      && ar.Session == session
                      && (ar.LeaveRequestId == null || ar.UpdatedByUserId != null)
                      && _context.ClassAssignments.Any(ca => ca.ClassId == ar.ClassId && ca.StudentId == ar.StudentId && ca.IsActive))
            .GroupBy(ar => ar.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Select(ar => ar.StudentId).Distinct().Count() })
            .ToListAsync(ct);

        return submittedCounts
            .Where(x => activeCounts.TryGetValue(x.ClassId, out var total) && total > 0 && x.Count >= total)
            .Select(x => x.ClassId)
            .ToHashSet();
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetStudentAttendanceHistoryAsync(
        int studentId, 
        DateTime fromDate, 
        DateTime toDate, 
        CancellationToken cancellationToken = default)
    {
        return await _context.AttendanceRecords
            .AsNoTracking()
            .Where(ar => ar.StudentId == studentId && ar.Date >= fromDate && ar.Date <= toDate)
            .OrderByDescending(ar => ar.Date)
            .ThenBy(ar => ar.Session)
            .ToListAsync(cancellationToken);
    }
}
