using Lease.Infrastructure.Persistence;
using LeaseApi.Endpoints;
using LeaseApi.Repositories;
using LeaseApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<LeaseDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<JobRepository>();
builder.Services.AddScoped<LeaseResultRepository>();
builder.Services.AddScoped<LeaseOrchestrator>();
builder.Services.AddHttpContextAccessor();

// Outbound call to LeaseParserFunction trigger endpoint with timeout
builder.Services.AddHttpClient<LeaseProcessingTrigger>(client=>{
     client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// ---- Cross-cutting middleware ----

// Correlation id for traceability across API, Azure Function & HMLR Api calls.
app.Use(async (ctx, next) =>
{
    const string header = "X-Correlation-ID";

    if (!ctx.Request.Headers.TryGetValue(header, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        correlationId = Guid.NewGuid().ToString("N");

    ctx.Response.Headers[header] = correlationId!;
    using (app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId! }))
    {
        await next();
    }
});


// Centralised exception handling (consistent ProblemDetails; avoids leaking internals).
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        app.Logger.LogError(exception, "Unhandled exception for {Path}", context.Request.Path);

        var problem = new ProblemDetails
        {
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = app.Environment.IsDevelopment() ? exception?.Message : "Please contact support with the correlation id."
        };

        context.Response.StatusCode = problem.Status.Value;
        await context.Response.WriteAsJsonAsync(problem);
    });
});

// Ensure DB exists for demo/local execution.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeaseDbContext>();
    db.Database.EnsureCreated();
}


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapLeaseEndpoints();

app.Run();