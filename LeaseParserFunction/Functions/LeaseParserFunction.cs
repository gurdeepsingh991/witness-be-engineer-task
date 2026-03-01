using LeaseParserFunction.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace LeaseParserFunction.Functions;


public class LeaseParserFunction
{
    private readonly LeaseProcessingService _service;
    public LeaseParserFunction(LeaseProcessingService service)
    {
        _service = service;
    }
    [Function("LeaseParser")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var correlationId =
            req.Headers.TryGetValues("X-Correlation-ID", out var values)
                ? values.FirstOrDefault()
                : Guid.NewGuid().ToString("N");

        var titleNumber = System.Web.HttpUtility
            .ParseQueryString(req.Url.Query)["titleNumber"];

        if (string.IsNullOrWhiteSpace(titleNumber))
        {
            Console.WriteLine($"[{correlationId}] Missing titleNumber");
            // TODO: Replace with structured logging
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing titleNumber");
            return bad;
        }

        Console.WriteLine($"[{correlationId}] Processing started for Title: {titleNumber}");
        // TODO: Replace with ILogger and correlation scope

        try
        {
            await _service.ProcessAsync(titleNumber);

            Console.WriteLine($"[{correlationId}] Processing completed successfully for Title: {titleNumber}");
            // TODO: Replace with structured success log

            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{correlationId}] ERROR processing Title: {titleNumber}");
            Console.WriteLine(ex.Message);
            // TODO: Replace with structured error logging

            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Processing failed");
            return error;
        }
    }
}