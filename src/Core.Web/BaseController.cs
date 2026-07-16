using Core.CQRS;
using Microsoft.AspNetCore.Mvc;

namespace Core.Web;

[ApiController]
[TypeFilter(typeof(ResultActionFilter))]
public abstract class BaseController(IRequestDispatcher requestDispatcher) : ControllerBase
{
    protected readonly IRequestDispatcher _requestDispatcher = requestDispatcher;
}
