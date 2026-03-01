// using System.Net.Http.Headers;
// using System.Text;
// using System.Text.Json;
// using System.Text.Json.Serialization;
// using Lease.Domain.Models;

// namespace Lease.Domain.Parsers;

// public class AILeaseParser : ILeaseParser
// {
//     private readonly HttpClient _http;
//     private readonly string _apiKey;

//     // Free models on OpenRouter — try in this order, all handle structured text well
//     // "meta-llama/llama-3.1-8b-instruct:free"  ← good and free
//     // "mistralai/mistral-7b-instruct:free"      ← solid fallback
//     // "google/gemma-2-9b-it:free"               ← also free
//     private const string Model = "meta-llama/llama-3.1-8b-instruct:free";

//     public AILeaseParser(HttpClient http, string apiKey)
//     {
//         _http = http;
//         _apiKey = apiKey;
//     }

//     public async IEnumerable<ParsedScheduleNoticeOfLease> ParseAsync(
//         IEnumerable<RawScheduleNoticeOfLease> rawItems)
//     {
//         // Run all entries in parallel then preserve order
//         var tasks = rawItems
//             .Select(raw => ParseSingleAsync(raw))
//             .ToList();

//         return await Task.WhenAll(tasks);
//     }

//     private async Task<ParsedScheduleNoticeOfLease> ParseSingleAsync(
//         RawScheduleNoticeOfLease raw)
//     {
//         var notes = raw.EntryText
//             .Where(l => l.TrimStart().StartsWith("NOTE"))
//             .ToList();

//         var dataLines = raw.EntryText
//             .Where(l => !l.TrimStart().StartsWith("NOTE"))
//             .ToList();

//         var linesText = string.Join("\n", dataLines.Select(l => $"  {l}"));

//         var request = new
//         {
//             model = Model,
//             messages = new[]
//             {
//                 new
//                 {
//                     role = "system",
//                     content = """
//                         You are a parser for HMLR Schedule of Notices of Leases.
//                         You will receive raw lines from a single lease entry in a
//                         fixed-width columnar format. The columns are:

//                         1. RegistrationDateAndPlanRef — starts with a date (dd.mm.yyyy),
//                            optionally followed by plan reference text like
//                            "Edged and numbered X in [colour] (part of)".
//                            Sometimes there is NO plan reference, just the date.
//                            This text can span multiple lines.

//                         2. PropertyDescription — the property address or description.
//                            May span multiple lines.

//                         3. DateOfLeaseAndTerm — starts with a date (dd.mm.yyyy) followed
//                            by the lease term e.g. "125 years from 1.1.2009" or
//                            "from 10 October 2018 to and including 19 April 2028".
//                            May span multiple lines.

//                         4. LesseesTitle — a short title number like TGL24029 or EGL557357.
//                            Always appears on the first line only.

//                         Respond ONLY with a valid JSON object. No markdown, no explanation,
//                         no extra text — just the raw JSON:
//                         {
//                           "registrationDateAndPlanRef": "...",
//                           "propertyDescription": "...",
//                           "dateOfLeaseAndTerm": "...",
//                           "lesseesTitle": "..."
//                         }
//                         """
//                 },
//                 new
//                 {
//                     role = "user",
//                     content = $"Parse these lines:\n{linesText}"
//                 }
//             },
//             temperature = 0,  // deterministic output
//             max_tokens = 300
//         };

//         var json = JsonSerializer.Serialize(request);
//         var httpRequest = new HttpRequestMessage(HttpMethod.Post,
//             "https://openrouter.ai/api/v1/chat/completions")
//         {
//             Content = new StringContent(json, Encoding.UTF8, "application/json")
//         };
//         httpRequest.Headers.Authorization =
//             new AuthenticationHeaderValue("Bearer", _apiKey);

//         var response = await _http.SendAsync(httpRequest);
//         response.EnsureSuccessStatusCode();

//         var responseJson = await response.Content.ReadAsStringAsync();
//         var completion   = JsonSerializer.Deserialize<OpenRouterResponse>(responseJson)!;
//         var content      = completion.Choices[0].Message.Content.Trim();

//         // Strip markdown code fences if the model adds them despite instructions
//         content = StripCodeFences(content);

//         var fields = JsonSerializer.Deserialize<AiParsedFields>(content)
//             ?? throw new InvalidOperationException(
//                 $"Could not deserialize AI response for entry {raw.EntryNumber}: {content}");

//         return new ParsedScheduleNoticeOfLease
//         {
//             EntryNumber                = int.Parse(raw.EntryNumber),
//             EntryDate                  = null,
//             RegistrationDateAndPlanRef = fields.RegistrationDateAndPlanRef ?? "",
//             PropertyDescription        = fields.PropertyDescription        ?? "",
//             DateOfLeaseAndTerm         = fields.DateOfLeaseAndTerm         ?? "",
//             LesseesTitle               = fields.LesseesTitle               ?? "",
//             Notes                      = notes.Count > 0 ? notes : null
//         };
//     }

//     private static string StripCodeFences(string text)
//     {
//         // Some models wrap JSON in ```json ... ``` despite being told not to
//         var match = System.Text.RegularExpressions.Regex.Match(
//             text, @"```(?:json)?\s*([\s\S]*?)```");
//         return match.Success ? match.Groups[1].Value.Trim() : text;
//     }

//     // ── Response shape (OpenAI-compatible, works with OpenRouter) ──────────

//     private record OpenRouterResponse(
//         [property: JsonPropertyName("choices")] List<Choice> Choices);

//     private record Choice(
//         [property: JsonPropertyName("message")] Message Message);

//     private record Message(
//         [property: JsonPropertyName("content")] string Content);

//     private record AiParsedFields(
//         [property: JsonPropertyName("registrationDateAndPlanRef")]
//         string? RegistrationDateAndPlanRef,
//         [property: JsonPropertyName("propertyDescription")]
//         string? PropertyDescription,
//         [property: JsonPropertyName("dateOfLeaseAndTerm")]
//         string? DateOfLeaseAndTerm,
//         [property: JsonPropertyName("lesseesTitle")]
//         string? LesseesTitle);
// }