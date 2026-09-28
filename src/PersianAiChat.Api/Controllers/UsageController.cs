using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersianAiChat.Application.Abstractions;

namespace PersianAiChat.Api.Controllers;

[ApiController]
[Route("api/usage")]
[Authorize]
public sealed class UsageController : ControllerBase
{
    private readonly IUsageService _usageService;

    public UsageController(IUsageService usageService) => _usageService = usageService;

    /// <summary>Returns cumulative token usage for the authenticated user.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var summary = await _usageService.GetSummaryAsync(userId, cancellationToken);
        return Ok(new
        {
            totalTokens = summary.TotalTokens,
            totalPromptTokens = summary.TotalPromptTokens,
            totalCompletionTokens = summary.TotalCompletionTokens,
            totalReasoningTokens = summary.TotalReasoningTokens,
            totalCost = summary.TotalCost,
            totalWebSearchRequests = summary.TotalWebSearchRequests
        });
    }
}
