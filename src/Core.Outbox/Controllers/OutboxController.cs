using Core.CQRS;
using Core.Outbox.Commands;
using Core.Outbox.Models;
using Core.Outbox.Queries;
using Core.Pager;
using Core.Permissions;
using Core.ResultPattern;
using Core.Web;
using Microsoft.AspNetCore.Mvc;

namespace Core.Outbox.Controllers;

[Route("api/outbox")]
[HasPermission(OutboxPermission.Manage)]
public class OutboxController(IRequestDispatcher requestDispatcher) : BaseController(requestDispatcher)
{
    [HttpGet("dead-letters")]
    public async Task<Result<PagedResult<DeadLetterListItem>>> GetDeadLetters(
        [FromQuery] int page = 1, [FromQuery] int pageSize = PagerSettings.MaxPageSize) =>
        await _requestDispatcher.Dispatch(new GetDeadLettersQuery(page, pageSize));

    [HttpPost("dead-letters/{id:guid}/replay")]
    public async Task<Result> ReplayDeadLetter(Guid id) =>
        await _requestDispatcher.Dispatch(new ReplayDeadLetterCommand(id));

    [HttpPost("dead-letters/{id:guid}/discard")]
    public async Task<Result> DiscardDeadLetter(Guid id) =>
        await _requestDispatcher.Dispatch(new DiscardDeadLetterCommand(id));
}
