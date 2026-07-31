using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace xsmbsocket.Shares
{
    public class ValidateModelFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(x => x.Value.Errors.Any())
                    .Select(x => new
                    {
                        Field = x.Key,
                        Messages = x.Value.Errors.Select(e => e.ErrorMessage)
                    });

                var response = new
                {
                    Status = false,
                    Message = "Validation failed",
                    Errors = context.ModelState.SelectMany(x => x.Value.Errors)
                        .Select(e => e.ErrorMessage).ToList()
                };

                context.Result = new BadRequestObjectResult(response);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}