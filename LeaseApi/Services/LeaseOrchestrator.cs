using System.Text.Json;
using System.Text.RegularExpressions;
using LeaseApi.Contracts;
using Lease.Domain.Enums;
using LeaseApi.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace LeaseApi.Services;

public sealed class LeaseOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly JobRepository _jobs;
    private readonly LeaseResultRepository _results;
    private readonly LeaseProcessingTrigger _trigger;
    private readonly ILogger<LeaseOrchestrator> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LeaseOrchestrator(
        JobRepository jobs,
        LeaseResultRepository results,
        LeaseProcessingTrigger trigger,
        ILogger<LeaseOrchestrator> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _jobs = jobs;
        _results = results;
        _trigger = trigger;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IResult> HandleAsync(string titleNumber, CancellationToken ct)
    {
        titleNumber = titleNumber.Trim();

        if (string.IsNullOrWhiteSpace(titleNumber))
            return Results.BadRequest(new ProblemDetails { Title = "titleNumber is required." });

        if (!Regex.IsMatch(titleNumber, @"^[A-Z]{1,3}\d{4,7}$"))
            return Results.BadRequest(new ProblemDetails { Title = "Invalid title number format." });

        // 1) Cache hit
        var cached = await _results.GetByTitleAsync(titleNumber, ct);
        if (cached != null)
        {
            var dto = JsonSerializer.Deserialize<object>(cached.PayloadJson, JsonOptions);
            return Results.Ok(dto);
        }

        // 2) Idempotent job creation
        var job = await _jobs.CreateIfMissingAsync(titleNumber, ct);

        if (job.Status == JobStatus.Failed)
        {
            return Results.Problem(
                title: "Lease processing failed.",
                detail: job.LastError ?? "Unknown processing error.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (job.Status == JobStatus.Pending)
        {
            job.Status = JobStatus.Processing;
            job.AttemptCount += 1;
            await _jobs.UpdateAsync(job, ct);

            var httpContext = _httpContextAccessor.HttpContext;
            var correlationId = httpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                                ?? httpContext?.Response.Headers["X-Correlation-ID"].FirstOrDefault();

            try
            {
                await _trigger.TriggerAsync(titleNumber, correlationId, ct);
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Failed;
                job.LastError = ex.Message;
                await _jobs.UpdateAsync(job, ct);

                _logger.LogWarning(ex, "Trigger failed for {TitleNumber}", titleNumber);

                return Results.Problem(
                    title: "Downstream processing trigger failed.",
                    detail: "Unable to start processing at this time.",
                    statusCode: StatusCodes.Status502BadGateway);
            }
        }

        return Results.Accepted($"/{titleNumber}", new LeaseStatusDto
        {
            TitleNumber = titleNumber,
            Status = job.Status
        });
    }
}