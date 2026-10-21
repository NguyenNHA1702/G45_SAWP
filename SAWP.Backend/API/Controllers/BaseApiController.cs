using Core.Constants;
using Core.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult ToActionResult<T>(ApiResponse<T> response)
    {
        if (response.Success)
        {
            return Ok(response);
        }

        var code = response.Code;
        if (code == SystemMessages.MSG01.Code || code == SystemMessages.MSG24.Code)
        {
            return BadRequest(response);
        }
        if (code == SystemMessages.MSG02.Code || code == SystemMessages.MSG03.Code || code == SystemMessages.MSG07.Code)
        {
            return Unauthorized(response);
        }
        if (code == SystemMessages.MSG10.Code || code == SystemMessages.MSG20.Code || code == SystemMessages.MSG42.Code)
        {
            return StatusCode(StatusCodes.Status403Forbidden, response);
        }
        if (code == SystemMessages.MSG11.Code || code == SystemMessages.MSG43.Code)
        {
            return NotFound(response);
        }
        if (code == SystemMessages.MSG09.Code || code == SystemMessages.MSG19.Code)
        {
            return Conflict(response);
        }

        return BadRequest(response);
    }
}
