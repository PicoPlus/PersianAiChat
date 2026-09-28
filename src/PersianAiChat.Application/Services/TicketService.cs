using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PersianAiChat.Application.Abstractions;

namespace PersianAiChat.Application.Services;

public sealed class TicketService : ITicketService
{
    private readonly IApplicationDbContext _db;
    private readonly IGapGptService _gapGpt;
    private readonly IHubSpotService _hubSpot;
    private readonly ILogger<TicketService> _logger;

    // Compact system prompt — ~60 tokens, no web search loaded
    private const string AnalysisSystemPrompt =
        "تو یک اپراتور پشتیبانی هستی. سوال کاربر و پاسخ دستیار را بخوان. " +
        "یک خلاصه ساختاریافته به فارسی بنویس که شامل: " +
        "۱) موضوع اصلی درخواست ۲) اقدام مورد نیاز از اپراتور ۳) اطلاعات کلیدی باشد. " +
        "خلاصه باید کمتر از ۲۵۰ کلمه باشد.";

    public TicketService(
        IApplicationDbContext db,
        IGapGptService gapGpt,
        IHubSpotService hubSpot,
        ILogger<TicketService> logger)
    {
        _db = db;
        _gapGpt = gapGpt;
        _hubSpot = hubSpot;
        _logger = logger;
    }

    public async Task<SubmitTicketResponse> SubmitAsync(
        SubmitTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Resolve user's HubSpot contact ID ──────────────────
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null || string.IsNullOrEmpty(user.HubSpotContactId))
        {
            _logger.LogWarning(
                "Ticket submission: user {UserId} has no HubSpot contact ID", request.UserId);
            return new SubmitTicketResponse(
                false, null, null, "اطلاعات حساب کاربری ناقص است. لطفاً با پشتیبانی تماس بگیرید.", null);
        }

        // ── 2. Call GapGPT.AnalyzeAsync — NO web_search ───────────
        //    Prompt: system + one user message = ~600 tokens total
        //    vs. a normal chat call = ~30,000 tokens
        var analysisMessages = new[]
        {
            new ChatMessage("system", AnalysisSystemPrompt),
            new ChatMessage("user",
                $"سوال کاربر:\n{request.UserQuestion}\n\n" +
                $"پاسخ دستیار:\n{Truncate(request.BotAnswer, 1500)}")
        };

        string analysisSummary;
        GapGptUsage analysisUsage;
        try
        {
            _logger.LogInformation(
                "Running ticket analysis for user {UserId} (AnalyzeAsync, no web_search)",
                request.UserId);

            var analysisResponse = await _gapGpt.AnalyzeAsync(analysisMessages, cancellationToken);
            analysisSummary = analysisResponse.Answer;
            analysisUsage   = analysisResponse.Usage;

            _logger.LogInformation(
                "Ticket analysis done. Tokens={Tokens} Cost={Cost} (no web search)",
                analysisUsage.TotalTokens, analysisUsage.Cost);
        }
        catch (Exception ex)
        {
            // Fallback: use the original question as analysis if GapGPT is down
            _logger.LogError(ex, "GapGPT ticket analysis failed — using fallback text");
            analysisSummary = request.UserQuestion;
            analysisUsage   = new GapGptUsage(0, 0, 0, 0, 0m, 0);
        }

        // ── 3. Build full ticket body ──────────────────────────────
        var ticketBody =
            $"📋 تحلیل دستیار هوشمند:\n{analysisSummary}\n\n" +
            $"━━━━━━━━━━━━━━━━━━━━━━━━\n" +
            $"سوال اصلی کاربر:\n{request.UserQuestion}\n\n" +
            $"پاسخ اولیه دستیار:\n{Truncate(request.BotAnswer, 800)}\n\n" +
            $"توکن مصرفی تحلیل: {analysisUsage.TotalTokens}";

        // ── 4. Create HubSpot Note + Deal (0 more tokens) ─────────
        var hubResult = await _hubSpot.CreateTicketAsync(
            user.HubSpotContactId,
            Truncate(request.UserQuestion, 100),
            ticketBody,
            request.Category,
            cancellationToken);

        return new SubmitTicketResponse(
            hubResult.Success,
            hubResult.TicketId,
            hubResult.NoteId,
            hubResult.Message,
            analysisSummary
        );
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
