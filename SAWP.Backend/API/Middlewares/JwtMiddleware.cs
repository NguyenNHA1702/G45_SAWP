using System.IdentityModel.Tokens.Jwt;

namespace API.Middlewares;

public class JwtMiddleware
{
    private readonly RequestDelegate _next;

    public JwtMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var jwt = handler.ReadJwtToken(token);
                    var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == "id" || c.Type == "nameid")?.Value;
                    if (int.TryParse(userIdClaim, out int userId))
                    {
                        context.Items["UserId"] = userId;
                    }
                }
            }
            catch { }
        }
        await _next(context);
    }
}
