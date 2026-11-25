using API.Extensions;
using Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

/// <summary>Parent account requests: Office Staff request (UC-67), System Administrators review (UC-68).</summary>
[ApiController]
[Route("api/account-requests")]
[Authorize]
public class AccountRequestsController : BaseApiController
{
    private readonly IAccountRequestService _service;

    public AccountRequestsController(IAccountRequestService service)
    {
        _service = service;
    }

    // ---- Office Staff (UC-67) ----

    /// <summary>Guardian records without an account, with the result of the checks (UC-67 step 1).</summary>
    [HttpGet("guardians-without-account")]
    [Authorize(Roles = "OfficeStaff")]
    public async Task<IActionResult> GuardiansWithoutAccount([FromQuery] string? search, CancellationToken ct)
        => ToActionResult(await _service.GetGuardiansWithoutAccountAsync(search, ct));

    /// <summary>Paged guardian records without an account, with the result of the checks.</summary>
    [HttpGet("guardians-without-account/paged")]
    [Authorize(Roles = "OfficeStaff")]
    public async Task<IActionResult> GuardiansWithoutAccountPaged(
        [FromQuery] string? search,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
        => ToActionResult(await _service.GetGuardiansWithoutAccountPagedAsync(search, pageIndex, pageSize, ct));

    /// <summary>Checks the rows of a request being prepared (UC-67 step 4, E1).</summary>
    [HttpPost("check")]
    [Authorize(Roles = "OfficeStaff")]
    public async Task<IActionResult> Check([FromBody] CheckAccountRequestRowsDto request, CancellationToken ct)
        => ToActionResult(await _service.CheckRowsAsync(request.GuardianIds, ct));

    /// <summary>Creates a request: a draft (A4) or submitted (step 5-6, MSG60).</summary>
    [HttpPost]
    [Authorize(Roles = "OfficeStaff")]
    public async Task<IActionResult> Create([FromBody] SaveAccountRequestDto request, CancellationToken ct)
        => ToActionResult(await _service.CreateRequestAsync(User.GetRequiredUserId(), request, ct));

    /// <summary>Changes or submits a draft.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "OfficeStaff")]
    public async Task<IActionResult> UpdateDraft(int id, [FromBody] SaveAccountRequestDto request, CancellationToken ct)
        => ToActionResult(await _service.UpdateDraftAsync(User.GetRequiredUserId(), id, request, ct));

    /// <summary>My Account Requests (A1).</summary>
    [HttpGet("mine")]
    [Authorize(Roles = "OfficeStaff")]
    public async Task<IActionResult> Mine(CancellationToken ct)
        => ToActionResult(await _service.GetMyRequestsAsync(User.GetRequiredUserId(), ct));

    /// <summary>My Account Requests (paged).</summary>
    [HttpGet("mine/paged")]
    [Authorize(Roles = "OfficeStaff")]
    public async Task<IActionResult> MinePaged(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ToActionResult(await _service.GetMyRequestsPagedAsync(User.GetRequiredUserId(), pageIndex, pageSize, ct));

    /// <summary>Puts rejected or failed rows into a new request linked to this one (A2).</summary>
    [HttpPost("{id:int}/resubmit")]
    [Authorize(Roles = "OfficeStaff")]
    public async Task<IActionResult> Resubmit(int id, [FromBody] AccountRequestRowsDto request, CancellationToken ct)
        => ToActionResult(await _service.ResubmitRowsAsync(User.GetRequiredUserId(), id, request, ct));

    /// <summary>Closes rejected or failed rows with a note (A2).</summary>
    [HttpPost("{id:int}/close-rows")]
    [Authorize(Roles = "OfficeStaff")]
    public async Task<IActionResult> CloseRows(int id, [FromBody] AccountRequestRowsDto request, CancellationToken ct)
        => ToActionResult(await _service.CloseRowsAsync(User.GetRequiredUserId(), id, request, ct));

    /// <summary>Resends the activation link, optionally to a corrected email (A3, UC-11 A2).</summary>
    [HttpPost("items/{itemId:int}/resend-activation")]
    [Authorize(Roles = "OfficeStaff")]
    public async Task<IActionResult> ResendActivation(int itemId, [FromBody] ResendActivationDto request, CancellationToken ct)
        => ToActionResult(await _service.ResendActivationAsync(User.GetRequiredUserId(), itemId, request, ct));

    // ---- Both (own requests for Office Staff, any submitted request for Administrators) ----

    [HttpGet("{id:int}")]
    [Authorize(Roles = "OfficeStaff,Admin")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
        => ToActionResult(await _service.GetRequestAsync(User.GetRequiredUserId(), User.IsInRole("Admin"), id, ct));

    // ---- System Administrator (UC-68) ----

    [HttpGet("review")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Review([FromQuery] string? status, CancellationToken ct)
        => ToActionResult(await _service.GetRequestsForReviewAsync(status, ct));

    [HttpGet("review/paged")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReviewPaged(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ToActionResult(await _service.GetRequestsForReviewPagedAsync(status, search, pageIndex, pageSize, ct));

    /// <summary>Approves or rejects rows; each approved row gets its own Pending Activation account (BR-67). MSG61 / MSG59.</summary>
    [HttpPost("{id:int}/decisions")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Decide(int id, [FromBody] AccountRequestDecisionDto decision, CancellationToken ct)
        => ToActionResult(await _service.DecideAsync(User.GetRequiredUserId(), id, decision, ct));
}
