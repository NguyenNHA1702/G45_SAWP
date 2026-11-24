using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.ExternalServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>
/// UC-67 Request Parent Accounts (Office Staff) and UC-68 Review Account Requests (System Administrator).
/// Parent accounts are created only from guardian records through an approved request (BR-66); each row is decided
/// and processed on its own (BR-67); a staff account is never a Parent account (BR-68).
/// </summary>
public class AccountRequestService : IAccountRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IAccountLinkService _accountLinks;
    private readonly IEmailService _emailService;
    private readonly ILogger<AccountRequestService> _logger;

    public AccountRequestService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IAccountLinkService accountLinks,
        IEmailService emailService,
        ILogger<AccountRequestService> logger)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _accountLinks = accountLinks;
        _emailService = emailService;
        _logger = logger;
    }

    #region Office Staff (UC-67)

    public async Task<ApiResponse<List<GuardianWithoutAccountDto>>> GetGuardiansWithoutAccountAsync(string? search, CancellationToken ct = default)
    {
        var q = search?.Trim().ToLower() ?? string.Empty;
        var query = GuardianQuery().Where(g => g.UserId == null);
        if (q.Length > 0)
        {
            query = query.Where(g => g.FullName.ToLower().Contains(q)
                                     || (g.Email != null && g.Email.ToLower().Contains(q))
                                     || g.StudentGuardians.Any(sg => sg.Student.StudentCode.ToLower().Contains(q) || sg.Student.FullName.ToLower().Contains(q)));
        }

        var guardians = await query.OrderBy(g => g.FullName).Take(200).ToListAsync(ct);
        var checks = await CheckAsync(guardians, null, ct);
        var items = guardians.Select(g =>
        {
            var check = checks[g.Id];
            return new GuardianWithoutAccountDto
            {
                GuardianId = g.Id,
                FullName = g.FullName,
                Email = g.Email,
                PhoneNumber = g.PhoneNumber,
                Students = StudentLabels(g),
                CheckResult = check.Result.ToString(),
                CheckMessage = check.Message,
                CanRequest = check.Result == AccountRequestCheckResult.Passed
            };
        }).ToList();
        return ApiResponse<List<GuardianWithoutAccountDto>>.Ok(items);
    }

    public async Task<ApiResponse<List<AccountRequestRowCheckDto>>> CheckRowsAsync(List<int> guardianIds, CancellationToken ct = default)
    {
        var ids = guardianIds.Distinct().ToList();
        var guardians = await GuardianQuery().Where(g => ids.Contains(g.Id)).ToListAsync(ct);
        var checks = await CheckAsync(OrderLike(guardians, ids), null, ct);
        return ApiResponse<List<AccountRequestRowCheckDto>>.Ok(OrderLike(guardians, ids).Select(g => ToCheckDto(g, checks[g.Id])).ToList());
    }

    public Task<ApiResponse<SaveAccountRequestResultDto>> CreateRequestAsync(int userId, SaveAccountRequestDto request, CancellationToken ct = default)
        => SaveAsync(userId, null, request, null, ct);

    public async Task<ApiResponse<SaveAccountRequestResultDto>> UpdateDraftAsync(int userId, int requestId, SaveAccountRequestDto request, CancellationToken ct = default)
    {
        var existing = await RequestQuery().FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (existing == null || existing.RequestedByUserId != userId)
        {
            return ApiResponse<SaveAccountRequestResultDto>.Fail(SystemMessages.MSG11);
        }
        if (existing.Status != AccountRequestStatus.Draft)
        {
            return ApiResponse<SaveAccountRequestResultDto>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ sửa được yêu cầu đang ở trạng thái nháp." });
        }
        return await SaveAsync(userId, existing, request, null, ct);
    }

    public async Task<ApiResponse<List<AccountRequestDto>>> GetMyRequestsAsync(int userId, CancellationToken ct = default)
    {
        var requests = await RequestQuery().Where(r => r.RequestedByUserId == userId)
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return ApiResponse<List<AccountRequestDto>>.Ok(requests.Select(r => ToDto(r, null)).ToList());
    }

    public async Task<ApiResponse<AccountRequestDto>> GetRequestAsync(int userId, bool isAdministrator, int requestId, CancellationToken ct = default)
    {
        var request = await RequestQuery().FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request == null || (!isAdministrator && request.RequestedByUserId != userId)
                            || (isAdministrator && request.Status == AccountRequestStatus.Draft && request.RequestedByUserId != userId))
        {
            return ApiResponse<AccountRequestDto>.Fail(SystemMessages.MSG11);
        }
        return ApiResponse<AccountRequestDto>.Ok(await ToDetailDtoAsync(request, ct));
    }

    public async Task<ApiResponse<AccountRequestDto>> CloseRowsAsync(int userId, int requestId, AccountRequestRowsDto request, CancellationToken ct = default)
    {
        var note = request.Note?.Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            return ApiResponse<AccountRequestDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập ghi chú khi đóng dòng." });
        }

        var entity = await RequestQuery().FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (entity == null || entity.RequestedByUserId != userId)
        {
            return ApiResponse<AccountRequestDto>.Fail(SystemMessages.MSG11);
        }

        var rows = entity.Items.Where(i => request.ItemIds.Contains(i.Id)).ToList();
        if (rows.Count == 0 || rows.Any(i => i.Status is not (AccountRequestItemStatus.Rejected or AccountRequestItemStatus.Failed)))
        {
            return ApiResponse<AccountRequestDto>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ đóng được các dòng bị từ chối hoặc lỗi." });
        }

        foreach (var row in rows)
        {
            row.Status = AccountRequestItemStatus.Closed;
            row.Reason = $"{row.Reason} | Đóng: {note}".Trim(' ', '|');
        }

        await _auditService.LogActionAsync(userId, AuditActions.AccountRequestCloseRows, nameof(AccountRequest), entity.Id.ToString(),
            null, new { ItemIds = rows.Select(r => r.Id), Note = note }, null, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApiResponse<AccountRequestDto>.Ok(await ToDetailDtoAsync(entity, ct), SystemMessages.MSG08);
    }

    public async Task<ApiResponse<SaveAccountRequestResultDto>> ResubmitRowsAsync(int userId, int requestId, AccountRequestRowsDto request, CancellationToken ct = default)
    {
        var original = await RequestQuery().FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (original == null || original.RequestedByUserId != userId)
        {
            return ApiResponse<SaveAccountRequestResultDto>.Fail(SystemMessages.MSG11);
        }

        var rows = original.Items.Where(i => request.ItemIds.Contains(i.Id)).ToList();
        if (rows.Count == 0 || rows.Any(i => i.Status is not (AccountRequestItemStatus.Rejected or AccountRequestItemStatus.Failed)))
        {
            return ApiResponse<SaveAccountRequestResultDto>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ gửi lại được các dòng bị từ chối hoặc lỗi." });
        }

        // A2: the rows go into a new request linked to the original one (checked again, e.g. after an email fix)
        var title = $"Gửi lại: {original.Title}";
        var result = await SaveAsync(userId, null, new SaveAccountRequestDto
        {
            Title = title.Length > 200 ? title[..200] : title,
            Reason = original.Reason.ToString(),
            Note = request.Note,
            GuardianIds = rows.Select(r => r.GuardianId).ToList(),
            Source = AccountRequestSource.Manual.ToString(),
            Submit = true
        }, original.Id, ct);

        if (result.Success)
        {
            var resubmitted = result.Data!.Request.Items?.Select(i => i.GuardianId).ToHashSet() ?? new HashSet<int>();
            foreach (var row in rows.Where(r => resubmitted.Contains(r.GuardianId)))
            {
                row.Status = AccountRequestItemStatus.Closed;
                row.Reason = $"{row.Reason} | Đã gửi lại trong yêu cầu #{result.Data.Request.Id}".Trim(' ', '|');
            }
            await _auditService.LogActionAsync(userId, AuditActions.AccountRequestResubmit, nameof(AccountRequest), original.Id.ToString(),
                null, new { NewRequestId = result.Data.Request.Id, GuardianIds = resubmitted }, null, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        return result;
    }

    public async Task<ApiResponse<AccountRequestItemDto>> ResendActivationAsync(int userId, int itemId, ResendActivationDto request, CancellationToken ct = default)
    {
        var item = await _unitOfWork.Repository<AccountRequestItem>().Query()
            .Include(i => i.AccountRequest)
            .Include(i => i.Guardian).ThenInclude(g => g.StudentGuardians).ThenInclude(sg => sg.Student)
            .Include(i => i.CreatedUser).ThenInclude(u => u!.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item == null || item.AccountRequest.RequestedByUserId != userId)
        {
            return ApiResponse<AccountRequestItemDto>.Fail(SystemMessages.MSG11);
        }
        var user = item.CreatedUser;
        if (user == null || item.Status is not (AccountRequestItemStatus.AccountCreated or AccountRequestItemStatus.EmailFailed)
                         || user.Status != AccountStatus.PendingActivation)
        {
            return ApiResponse<AccountRequestItemDto>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ gửi lại liên kết cho tài khoản đang chờ kích hoạt." });
        }

        // UC-11 A2: Office Staff may correct the email of an account that is still waiting for activation
        var newEmail = request.Email?.Trim();
        if (!string.IsNullOrEmpty(newEmail) && !string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (!GuardianMatcher.IsValidEmail(newEmail))
            {
                return ApiResponse<AccountRequestItemDto>.Fail(SystemMessages.MSG01, new List<string> { "Email không hợp lệ." });
            }
            var lower = newEmail.ToLowerInvariant();
            var taken = await _unitOfWork.Users.Query()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Where(u => u.Id != user.Id && (u.Email.ToLower() == lower || u.Username.ToLower() == lower))
                .ToListAsync(ct);
            if (taken.Count > 0)
            {
                var message = taken.Any(IsStaff)
                    ? "Email thuộc một tài khoản nhân sự; tài khoản phụ huynh cần một email khác (BR-68)."
                    : "Email đã được dùng cho một tài khoản khác.";
                return ApiResponse<AccountRequestItemDto>.Fail(SystemMessages.MSG01, new List<string> { message });
            }

            var oldEmail = user.Email;
            if (string.Equals(user.Username, oldEmail, StringComparison.OrdinalIgnoreCase))
            {
                user.Username = newEmail; // the login name of an account from a request is its email
            }
            user.Email = newEmail;
            user.UpdatedAt = DateTime.UtcNow;
            item.Guardian.Email = newEmail;
            await _auditService.LogActionAsync(userId, AuditActions.AccountRequestResendActivation, nameof(User), user.Id.ToString(),
                new { Email = oldEmail }, new { Email = newEmail }, null, ct);
        }
        else
        {
            await _auditService.LogActionAsync(userId, AuditActions.AccountRequestResendActivation, nameof(User), user.Id.ToString(),
                null, new { user.Email }, null, ct);
        }
        await _unitOfWork.SaveChangesAsync(ct);

        var sent = await _accountLinks.SendActivationLinkAsync(user, ct);
        item.Status = sent ? AccountRequestItemStatus.AccountCreated : AccountRequestItemStatus.EmailFailed;
        item.Reason = sent ? null : "Không gửi được email kích hoạt.";
        await _unitOfWork.SaveChangesAsync(ct);

        var dto = ToItemDto(item, null);
        return sent
            ? ApiResponse<AccountRequestItemDto>.Ok(dto, SystemMessages.MSG08)
            : ApiResponse<AccountRequestItemDto>.Fail(SystemMessages.MSG59);
    }

    private async Task<ApiResponse<SaveAccountRequestResultDto>> SaveAsync(
        int userId, AccountRequest? draft, SaveAccountRequestDto request, int? followsUpRequestId, CancellationToken ct)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        if (title.Length == 0 || title.Length > 200)
        {
            return ApiResponse<SaveAccountRequestResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Tiêu đề yêu cầu là bắt buộc (tối đa 200 ký tự)." });
        }
        if (!Enum.TryParse<AccountRequestReason>(request.Reason, true, out var reason) || !Enum.IsDefined(reason))
        {
            return ApiResponse<SaveAccountRequestResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn lý do của yêu cầu." });
        }
        var ids = request.GuardianIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return ApiResponse<SaveAccountRequestResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Yêu cầu cần ít nhất một người giám hộ." });
        }
        _ = Enum.TryParse<AccountRequestSource>(request.Source, true, out var source);

        var guardians = OrderLike(await GuardianQuery().Where(g => ids.Contains(g.Id)).ToListAsync(ct), ids);
        if (guardians.Count != ids.Count)
        {
            return ApiResponse<SaveAccountRequestResultDto>.Fail(SystemMessages.MSG11);
        }
        var checks = await CheckAsync(guardians, draft?.Id, ct);

        var entity = draft ?? new AccountRequest { RequestedByUserId = userId, CreatedAt = DateTime.UtcNow, FollowsUpRequestId = followsUpRequestId };
        entity.Title = title;
        entity.Reason = reason;
        entity.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        entity.Source = source == 0 ? AccountRequestSource.Manual : source;
        entity.SourceReference = string.IsNullOrWhiteSpace(request.SourceReference) ? null : request.SourceReference.Trim();

        var excluded = new List<AccountRequestRowCheckDto>();
        var rowGuardians = guardians;
        if (request.Submit)
        {
            // E1: rows that fail a check are left out of the submitted request
            excluded = guardians.Where(g => checks[g.Id].Result != AccountRequestCheckResult.Passed)
                .Select(g => ToCheckDto(g, checks[g.Id])).ToList();
            rowGuardians = guardians.Where(g => checks[g.Id].Result == AccountRequestCheckResult.Passed).ToList();
            if (rowGuardians.Count == 0)
            {
                return ApiResponse<SaveAccountRequestResultDto>.Fail(SystemMessages.MSG01, excluded.Select(e => $"{e.FullName}: {e.Message}").ToList());
            }
        }

        // Rows are rebuilt from the current selection (a draft may have changed)
        foreach (var old in entity.Items.ToList())
        {
            await _unitOfWork.Repository<AccountRequestItem>().DeleteAsync(old, ct);
        }
        entity.Items = rowGuardians.Select(g => new AccountRequestItem
        {
            AccountRequest = entity,
            GuardianId = g.Id,
            Guardian = g,
            Email = g.Email?.Trim(),
            CheckResult = checks[g.Id].Result,
            Status = AccountRequestItemStatus.Waiting
        }).ToList();

        if (request.Submit)
        {
            entity.Status = AccountRequestStatus.Submitted;
            entity.SubmittedAt = DateTime.UtcNow;
        }
        else
        {
            entity.Status = AccountRequestStatus.Draft;
        }

        if (draft == null)
        {
            await _unitOfWork.Repository<AccountRequest>().AddAsync(entity, ct);
        }
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.LogActionAsync(userId, request.Submit ? AuditActions.AccountRequestSubmit : AuditActions.AccountRequestSaveDraft,
            nameof(AccountRequest), entity.Id.ToString(), null,
            new { entity.Title, Reason = entity.Reason.ToString(), GuardianIds = rowGuardians.Select(g => g.Id), Excluded = excluded.Select(e => e.GuardianId), entity.FollowsUpRequestId },
            null, ct);

        if (request.Submit)
        {
            // Step 6: every active System Administrator is notified
            var requester = await _unitOfWork.Users.GetByIdAsync(userId, ct);
            foreach (var adminId in await ActiveAdministratorIdsAsync(ct))
            {
                await _unitOfWork.Repository<Notification>().AddAsync(new Notification
                {
                    UserId = adminId,
                    Type = "ACCOUNT_REQUEST",
                    Title = "Yêu cầu cấp tài khoản phụ huynh mới",
                    Content = $"{requester?.FullName ?? "Giáo vụ"} đã gửi yêu cầu \"{entity.Title}\" ({entity.Items.Count} người giám hộ).",
                    LinkUrl = $"/admin/account-requests?id={entity.Id}",
                    ReferenceKey = $"ACCOUNT_REQUEST:{entity.Id}",
                    CreatedAt = DateTime.UtcNow
                }, ct);
            }
        }
        await _unitOfWork.SaveChangesAsync(ct);

        var dto = await ToDetailDtoAsync(entity, ct);
        return ApiResponse<SaveAccountRequestResultDto>.Ok(new SaveAccountRequestResultDto { Request = dto, ExcludedRows = excluded },
            request.Submit ? SystemMessages.MSG60 : SystemMessages.MSG08);
    }

    #endregion

    #region System Administrator (UC-68)

    public async Task<ApiResponse<List<AccountRequestDto>>> GetRequestsForReviewAsync(string? status, CancellationToken ct = default)
    {
        var query = RequestQuery().Where(r => r.Status != AccountRequestStatus.Draft);
        if (Enum.TryParse<AccountRequestStatus>(status, true, out var filter))
        {
            query = query.Where(r => r.Status == filter);
        }
        var requests = await query.OrderByDescending(r => r.SubmittedAt).ToListAsync(ct);
        return ApiResponse<List<AccountRequestDto>>.Ok(requests.Select(r => ToDto(r, null)).ToList());
    }

    public async Task<ApiResponse<AccountRequestDecisionResultDto>> DecideAsync(int adminUserId, int requestId, AccountRequestDecisionDto decision, CancellationToken ct = default)
    {
        var request = await RequestQuery().FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request == null || request.Status == AccountRequestStatus.Draft)
        {
            return ApiResponse<AccountRequestDecisionResultDto>.Fail(SystemMessages.MSG11);
        }
        if (request.Status is not (AccountRequestStatus.Submitted or AccountRequestStatus.InReview))
        {
            return ApiResponse<AccountRequestDecisionResultDto>.Fail(SystemMessages.MSG09, new List<string> { "Yêu cầu này đã được xử lý xong." });
        }

        var waiting = request.Items.Where(i => i.Status == AccountRequestItemStatus.Waiting).ToList();
        var checks = await CheckAsync(waiting.Select(i => i.Guardian).ToList(), request.Id, ct);

        var toReject = decision.RejectAll ? waiting : waiting.Where(i => decision.RejectItemIds.Contains(i.Id)).ToList();
        var toApprove = decision.RejectAll
            ? new List<AccountRequestItem>()
            : decision.ApproveAllValid
                ? waiting.Where(i => checks[i.GuardianId].Result == AccountRequestCheckResult.Passed && !toReject.Contains(i)).ToList()
                : waiting.Where(i => decision.ApproveItemIds.Contains(i.Id) && !toReject.Contains(i)).ToList();

        var reason = decision.RejectReason?.Trim();
        if (toReject.Count > 0 && string.IsNullOrWhiteSpace(reason))
        {
            return ApiResponse<AccountRequestDecisionResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập lý do từ chối." });
        }
        if (toReject.Count == 0 && toApprove.Count == 0
            && !decision.ApproveAllValid && decision.ApproveItemIds.Count == 0)
        {
            return ApiResponse<AccountRequestDecisionResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn các dòng cần duyệt hoặc từ chối." });
        }

        var now = DateTime.UtcNow;
        var result = new AccountRequestDecisionResultDto();
        request.Status = AccountRequestStatus.InReview;

        foreach (var row in toReject)
        {
            row.Status = AccountRequestItemStatus.Rejected;
            row.Reason = reason;
            row.DecidedByUserId = adminUserId;
            row.DecidedAt = now;
            result.Rejected++;
        }
        await _unitOfWork.SaveChangesAsync(ct);

        // E1: a row that fails a check cannot be approved; it stays Waiting and is reported
        var approvable = new List<AccountRequestItem>();
        foreach (var row in toApprove)
        {
            var check = checks[row.GuardianId];
            if (check.Result == AccountRequestCheckResult.Passed)
            {
                approvable.Add(row);
            }
            else
            {
                result.NotApproved.Add(ToCheckDto(row.Guardian, check));
            }
        }

        // E3: when the Email Service is down the accounts are still created and the rows marked Email failed
        var emailAvailable = approvable.Count == 0 || await _emailService.IsAvailableAsync(ct);
        var parentRoleId = approvable.Count == 0 ? 0 : await _unitOfWork.Repository<Role>().Query()
            .Where(r => r.Name == UserRoles.Parent.ToString()).Select(r => r.Id).FirstOrDefaultAsync(ct);

        foreach (var row in approvable)
        {
            await ApproveRowAsync(row, adminUserId, parentRoleId, emailAvailable, result, ct);
        }

        var finished = request.Items.All(i => i.Status != AccountRequestItemStatus.Waiting);
        if (finished)
        {
            request.Status = FinalStatus(request.Items);
            request.CompletedAt = now;
        }

        await _auditService.LogActionAsync(adminUserId, AuditActions.AccountRequestDecide, nameof(AccountRequest), request.Id.ToString(),
            null, new
            {
                Approved = approvable.Select(r => new { r.Id, r.GuardianId, Status = r.Status.ToString(), r.CreatedUserId }),
                Rejected = toReject.Select(r => r.Id),
                Reason = reason,
                NotApproved = result.NotApproved.Select(n => n.GuardianId),
                Status = request.Status.ToString()
            }, null, ct);

        if (finished)
        {
            // Step 5: the requesting Office Staff member gets the counts
            await _unitOfWork.Repository<Notification>().AddAsync(new Notification
            {
                UserId = request.RequestedByUserId,
                Type = "ACCOUNT_REQUEST",
                Title = "Yêu cầu cấp tài khoản đã được xử lý",
                Content = $"Yêu cầu \"{request.Title}\": {Count(request, AccountRequestItemStatus.AccountCreated, AccountRequestItemStatus.EmailFailed, AccountRequestItemStatus.Active)} tài khoản đã tạo, "
                          + $"{Count(request, AccountRequestItemStatus.Rejected)} bị từ chối, {Count(request, AccountRequestItemStatus.Failed)} lỗi.",
                LinkUrl = $"/office/account-requests?id={request.Id}",
                ReferenceKey = $"ACCOUNT_REQUEST_DONE:{request.Id}",
                CreatedAt = now
            }, ct);
        }
        await _unitOfWork.SaveChangesAsync(ct);

        result.Request = await ToDetailDtoAsync(request, ct);
        return ApiResponse<AccountRequestDecisionResultDto>.Ok(result,
            result.EmailFailed > 0 && !emailAvailable ? SystemMessages.MSG59 : SystemMessages.MSG61);
    }

    /// <summary>Step 4 for one row (BR-67): its own account, saved on its own; a failure only marks this row (E2).</summary>
    private async Task ApproveRowAsync(AccountRequestItem row, int adminUserId, int parentRoleId, bool emailAvailable,
        AccountRequestDecisionResultDto result, CancellationToken ct)
    {
        var guardian = row.Guardian;
        var email = guardian.Email!.Trim();
        var user = new User
        {
            Username = email,       // the guardian's email address is the login (UC-68 step 4)
            Email = email,
            FullName = guardian.FullName,
            PhoneNumber = guardian.PhoneNumber,
            PasswordHash = string.Empty,
            Status = AccountStatus.PendingActivation,
            CreatedAt = DateTime.UtcNow
        };
        if (parentRoleId > 0)
        {
            user.UserRoles.Add(new UserRole { RoleId = parentRoleId, AssignedAt = DateTime.UtcNow });
        }

        try
        {
            await _unitOfWork.Repository<User>().AddAsync(user, ct);
            guardian.User = user; // linked to its guardian record at creation (BR-66)
            row.CreatedUser = user;
            row.Status = AccountRequestItemStatus.AccountCreated;
            row.Reason = null;
            row.DecidedByUserId = adminUserId;
            row.DecidedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Account request row {ItemId}: account could not be created", row.Id);
            // Discard the unsaved account and its role so that the next rows can still be saved
            foreach (var role in user.UserRoles.ToList())
            {
                await _unitOfWork.Repository<UserRole>().DeleteAsync(role, ct);
            }
            await _unitOfWork.Repository<User>().DeleteAsync(user, ct);
            guardian.User = null;
            guardian.UserId = null;
            row.CreatedUser = null;
            row.CreatedUserId = null;
            row.Status = AccountRequestItemStatus.Failed;
            row.Reason = "Không tạo được tài khoản (có thể email vừa được dùng cho tài khoản khác).";
            row.DecidedByUserId = adminUserId;
            row.DecidedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);
            result.Failed++;
            return;
        }

        result.AccountsCreated++;
        var sent = emailAvailable && await _accountLinks.SendActivationLinkAsync(user, ct);
        if (!sent)
        {
            row.Status = AccountRequestItemStatus.EmailFailed;
            row.Reason = "Không gửi được email kích hoạt; Giáo vụ có thể gửi lại liên kết.";
            await _unitOfWork.SaveChangesAsync(ct);
            result.EmailFailed++;
        }
    }

    private static AccountRequestStatus FinalStatus(ICollection<AccountRequestItem> items)
    {
        var decided = items.Where(i => i.Status != AccountRequestItemStatus.Closed).ToList();
        if (decided.Count > 0 && decided.All(i => i.Status == AccountRequestItemStatus.Rejected))
        {
            return AccountRequestStatus.Rejected;
        }
        return decided.Any(i => i.Status is AccountRequestItemStatus.Rejected or AccountRequestItemStatus.Failed)
            ? AccountRequestStatus.PartlyCompleted
            : AccountRequestStatus.Completed;
    }

    private static int Count(AccountRequest request, params AccountRequestItemStatus[] statuses) =>
        request.Items.Count(i => statuses.Contains(i.Status));

    #endregion

    #region Checks (UC-67 E1, UC-68 step 2, BR-64, BR-68)

    private sealed record RowCheck(AccountRequestCheckResult Result, string? Message);

    /// <summary>Checks each guardian; <paramref name="ownRequestId"/>'s own rows do not count as "in an open request".</summary>
    private async Task<Dictionary<int, RowCheck>> CheckAsync(IReadOnlyCollection<Guardian> guardians, int? ownRequestId, CancellationToken ct)
    {
        var ids = guardians.Select(g => g.Id).ToList();
        var emails = guardians.Select(g => GuardianMatcher.NormalizeEmail(g.Email)).Where(e => e.Length > 0).Distinct().ToList();

        var inOpenRequest = (await _unitOfWork.Repository<AccountRequestItem>().Query()
                .Where(i => ids.Contains(i.GuardianId) && i.Status == AccountRequestItemStatus.Waiting
                            && (i.AccountRequest.Status == AccountRequestStatus.Submitted || i.AccountRequest.Status == AccountRequestStatus.InReview)
                            && (ownRequestId == null || i.AccountRequestId != ownRequestId))
                .Select(i => i.GuardianId)
                .ToListAsync(ct))
            .ToHashSet();

        var accounts = emails.Count == 0
            ? new List<User>()
            : await _unitOfWork.Users.Query()
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Where(u => emails.Contains(u.Email.ToLower()) || emails.Contains(u.Username.ToLower()))
                .ToListAsync(ct);

        var seen = new HashSet<string>();
        var result = new Dictionary<int, RowCheck>();
        foreach (var g in guardians)
        {
            var email = GuardianMatcher.NormalizeEmail(g.Email);
            RowCheck check;
            if (g.UserId != null)
            {
                check = new(AccountRequestCheckResult.AlreadyHasAccount, "Người giám hộ đã có tài khoản.");
            }
            else if (email.Length == 0)
            {
                check = new(AccountRequestCheckResult.MissingEmail, "Hồ sơ người giám hộ chưa có email (BR-64).");
            }
            else if (!GuardianMatcher.IsValidEmail(g.Email))
            {
                check = new(AccountRequestCheckResult.InvalidEmail, "Email không hợp lệ.");
            }
            else if (accounts.Where(a => a.Email.ToLower() == email || a.Username.ToLower() == email).ToList() is { Count: > 0 } owners)
            {
                check = owners.Any(IsStaff)
                    ? new(AccountRequestCheckResult.EmailOfStaffAccount,
                        "Email thuộc một tài khoản nhân sự; tài khoản phụ huynh cần một email khác. Hãy sửa email trong hồ sơ người giám hộ rồi gửi lại (BR-68).")
                    : new(AccountRequestCheckResult.EmailInUse, "Email đã được dùng cho một tài khoản khác.");
            }
            else if (inOpenRequest.Contains(g.Id))
            {
                check = new(AccountRequestCheckResult.InOpenRequest, "Người giám hộ đang nằm trong một yêu cầu chưa xử lý xong.");
            }
            else if (!seen.Add(email))
            {
                check = new(AccountRequestCheckResult.DuplicateInRequest, "Email này xuất hiện hai lần trong yêu cầu.");
            }
            else
            {
                check = new(AccountRequestCheckResult.Passed, null);
            }
            result[g.Id] = check;
        }
        return result;
    }

    /// <summary>A staff account is an account with any role other than Parent (BR-68).</summary>
    private static bool IsStaff(User user) =>
        user.UserRoles.Any(ur => ur.Role != null && !ur.Role.Name.Equals(UserRoles.Parent.ToString(), StringComparison.OrdinalIgnoreCase));

    private async Task<List<int>> ActiveAdministratorIdsAsync(CancellationToken ct) =>
        await _unitOfWork.Users.Query()
            .Where(u => u.Status == AccountStatus.Active && u.UserRoles.Any(ur => ur.Role.Name == UserRoles.Admin.ToString()))
            .Select(u => u.Id)
            .ToListAsync(ct);

    #endregion

    #region Queries and mapping

    private IQueryable<Guardian> GuardianQuery() => _unitOfWork.Repository<Guardian>().Query()
        .Include(g => g.StudentGuardians).ThenInclude(sg => sg.Student).ThenInclude(s => s.ClassAssignments).ThenInclude(ca => ca.Class);

    private IQueryable<AccountRequest> RequestQuery() => _unitOfWork.Repository<AccountRequest>().Query()
        .Include(r => r.RequestedByUser)
        .Include(r => r.Items).ThenInclude(i => i.Guardian).ThenInclude(g => g.StudentGuardians).ThenInclude(sg => sg.Student)
            .ThenInclude(s => s.ClassAssignments).ThenInclude(ca => ca.Class)
        .Include(r => r.Items).ThenInclude(i => i.CreatedUser)
        .Include(r => r.Items).ThenInclude(i => i.DecidedByUser);

    private static List<Guardian> OrderLike(List<Guardian> guardians, List<int> ids) =>
        ids.Select(id => guardians.FirstOrDefault(g => g.Id == id)).Where(g => g != null).Select(g => g!).ToList();

    private static List<string> StudentLabels(Guardian g) => g.StudentGuardians
        .Where(sg => sg.Student != null)
        .Select(sg =>
        {
            var cls = sg.Student.ClassAssignments?.FirstOrDefault(ca => ca.IsActive)?.Class?.ClassName;
            return $"{sg.Student.StudentCode} {sg.Student.FullName}{(cls != null ? $" ({cls})" : string.Empty)}";
        })
        .ToList();

    private static AccountRequestRowCheckDto ToCheckDto(Guardian g, RowCheck check) => new()
    {
        GuardianId = g.Id,
        FullName = g.FullName,
        Email = g.Email,
        CheckResult = check.Result.ToString(),
        Message = check.Message
    };

    private async Task<AccountRequestDto> ToDetailDtoAsync(AccountRequest request, CancellationToken ct)
    {
        var open = request.Items.Where(i => i.Status == AccountRequestItemStatus.Waiting || request.Status == AccountRequestStatus.Draft).ToList();
        var checks = open.Count == 0 ? new Dictionary<int, RowCheck>() : await CheckAsync(open.Select(i => i.Guardian).ToList(), request.Id, ct);
        return ToDto(request, checks);
    }

    private static AccountRequestDto ToDto(AccountRequest r, Dictionary<int, RowCheck>? checks)
    {
        var statuses = r.Items.Select(EffectiveStatus).ToList();
        return new AccountRequestDto
        {
            Id = r.Id,
            Title = r.Title,
            Reason = r.Reason.ToString(),
            Note = r.Note,
            Source = r.Source.ToString(),
            SourceReference = r.SourceReference,
            Status = r.Status.ToString(),
            RequestedByUserId = r.RequestedByUserId,
            RequestedByName = r.RequestedByUser?.FullName ?? string.Empty,
            FollowsUpRequestId = r.FollowsUpRequestId,
            CreatedAt = r.CreatedAt,
            SubmittedAt = r.SubmittedAt,
            CompletedAt = r.CompletedAt,
            TotalRows = statuses.Count,
            WaitingRows = statuses.Count(s => s == AccountRequestItemStatus.Waiting),
            CreatedRows = statuses.Count(s => s is AccountRequestItemStatus.AccountCreated or AccountRequestItemStatus.Active or AccountRequestItemStatus.EmailFailed),
            RejectedRows = statuses.Count(s => s == AccountRequestItemStatus.Rejected),
            FailedRows = statuses.Count(s => s == AccountRequestItemStatus.Failed),
            ClosedRows = statuses.Count(s => s == AccountRequestItemStatus.Closed),
            Items = checks == null ? null : r.Items.OrderBy(i => i.Id).Select(i => ToItemDto(i, checks)).ToList()
        };
    }

    private static AccountRequestItemDto ToItemDto(AccountRequestItem i, Dictionary<int, RowCheck>? checks)
    {
        RowCheck? check = null;
        checks?.TryGetValue(i.GuardianId, out check);
        return new AccountRequestItemDto
        {
            Id = i.Id,
            GuardianId = i.GuardianId,
            GuardianName = i.Guardian?.FullName ?? string.Empty,
            Email = i.Email,
            CurrentEmail = i.Guardian?.Email,
            Students = i.Guardian == null ? new List<string>() : StudentLabels(i.Guardian),
            Status = EffectiveStatus(i).ToString(),
            CheckResult = check?.Result.ToString(),
            CheckMessage = check?.Message,
            Reason = i.Reason,
            CreatedUserId = i.CreatedUserId,
            DecidedByName = i.DecidedByUser?.FullName,
            DecidedAt = i.DecidedAt
        };
    }

    /// <summary>A created account shows as Active once its holder has set the password (UC-67 A1).</summary>
    private static AccountRequestItemStatus EffectiveStatus(AccountRequestItem i) =>
        i.Status is AccountRequestItemStatus.AccountCreated or AccountRequestItemStatus.EmailFailed
        && i.CreatedUser?.Status == AccountStatus.Active
            ? AccountRequestItemStatus.Active
            : i.Status;

    #endregion
}
