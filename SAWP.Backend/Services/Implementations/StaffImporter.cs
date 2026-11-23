using Core.Enums;
using ClosedXML.Excel;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class StaffImporter : IStaffImporter
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountLinkService _accountLinks;

    public StaffImporter(IUnitOfWork unitOfWork, IAccountLinkService accountLinks)
    {
        _unitOfWork = unitOfWork;
        _accountLinks = accountLinks;
    }

    public async Task<ApiResponse<int>> ImportStaffAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        if (fileStream == null || fileStream.Length == 0)
        {
            return ApiResponse<int>.Fail(SystemMessages.MSG41);
        }

        try
        {
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                return ApiResponse<int>.Fail(SystemMessages.MSG41);
            }

            var roles = await _unitOfWork.Repository<Role>().Query().ToListAsync(cancellationToken);
            var existingUsers = await _unitOfWork.Repository<User>().Query()
                .Select(u => new { u.Username, u.Email })
                .ToListAsync(cancellationToken);
            var existingUsernames = existingUsers.Select(u => u.Username.ToLower()).ToHashSet();
            var existingEmails = existingUsers.Select(u => u.Email.ToLower()).ToHashSet();

            var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) ?? Enumerable.Empty<IXLRangeRow>();
            int importedCount = 0;
            var createdUsers = new List<User>();

            foreach (var row in rows)
            {
                var username = row.Cell(1).GetString().Trim();
                var fullName = row.Cell(2).GetString().Trim();
                var email = row.Cell(3).GetString().Trim();
                var phone = row.Cell(4).GetString().Trim();
                var roleName = row.Cell(5).GetString().Trim();
                var staffCode = row.Cell(6).GetString().Trim();
                var position = row.Cell(7).GetString().Trim();
                var department = row.Cell(8).GetString().Trim();
                // Column 9 (password) of older templates is ignored: no password is carried in an import file (BR-63)

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
                    continue;

                if (existingUsernames.Contains(username.ToLower()) || existingEmails.Contains(email.ToLower()))
                    continue;

                var matchedRole = roles.FirstOrDefault(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase))
                    ?? roles.FirstOrDefault(r => r.Name.Equals("Teacher", StringComparison.OrdinalIgnoreCase));

                if (matchedRole == null) continue;

                var user = new User
                {
                    Username = username,
                    FullName = fullName,
                    Email = email,
                    PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone,
                    PasswordHash = string.Empty,
                    Status = AccountStatus.PendingActivation, // activated by the holder through the emailed link
                    CreatedAt = DateTime.UtcNow,
                    UserRoles = new List<UserRole> { new() { RoleId = matchedRole.Id } },
                    StaffProfile = new StaffProfile
                    {
                        StaffCode = string.IsNullOrWhiteSpace(staffCode) ? $"STF{DateTime.UtcNow:HHmmss}{importedCount}" : staffCode,
                        Position = string.IsNullOrWhiteSpace(position) ? matchedRole.Name : position,
                        Department = string.IsNullOrWhiteSpace(department) ? "Nhà trường" : department,
                        IsHomeroomTeacher = matchedRole.Name.Equals("Teacher", StringComparison.OrdinalIgnoreCase),
                        CreatedAt = DateTime.UtcNow
                    }
                };

                await _unitOfWork.Repository<User>().AddAsync(user, cancellationToken);
                createdUsers.Add(user);
                existingUsernames.Add(username.ToLower());
                existingEmails.Add(email.ToLower());
                importedCount++;
            }

            if (importedCount == 0)
            {
                return ApiResponse<int>.Ok(0, SystemMessages.MSG49);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var notSent = new List<string>();
            foreach (var created in createdUsers)
            {
                if (!await _accountLinks.SendActivationLinkAsync(created, cancellationToken))
                {
                    notSent.Add(created.Email);
                }
            }

            var result = ApiResponse<int>.Ok(importedCount, SystemMessages.MSG48);
            if (notSent.Count > 0)
            {
                // The accounts exist; each holder can ask for a new link with "Quên mật khẩu" (UC-66 A3)
                result.Errors = new List<string> { $"{SystemMessages.MSG59.Content} ({notSent.Count} tài khoản chưa nhận được email kích hoạt)" };
            }
            return result;
        }
        catch
        {
            return ApiResponse<int>.Fail(SystemMessages.MSG41);
        }
    }
}
