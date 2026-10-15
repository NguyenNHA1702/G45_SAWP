using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Middlewares;

public class DataScopeFilter : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await next();
    }
}
