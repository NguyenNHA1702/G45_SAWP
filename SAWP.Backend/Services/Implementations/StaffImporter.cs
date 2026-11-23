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

    public StaffImporter(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
                var rawPassword = row.Cell(9).GetString().Trim();

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
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(string.IsNullOrWhiteSpace(rawPassword) ? "Password@123" : rawPassword),
                    Status = AccountStatus.Active,
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
                existingUsernames.Add(username.ToLower());
                existingEmails.Add(email.ToLower());
                importedCount++;
            }

            if (importedCount > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return ApiResponse<int>.Ok(importedCount, importedCount > 0 ? SystemMessages.MSG48 : SystemMessages.MSG49);
        }
        catch
        {
            return ApiResponse<int>.Fail(SystemMessages.MSG41);
        }
    }
}
