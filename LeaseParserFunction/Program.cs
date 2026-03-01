using Lease.Domain.Parsers;
using Lease.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using LeaseParserFunction.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // Database
        var connectionString = context.Configuration["ConnectionStrings:Default"];

        services.AddDbContext<LeaseDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddHttpClient<HmlrClient>((sp, client) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var baseUrl = config["Hmlr:BaseUrl"];

                if (!string.IsNullOrWhiteSpace(baseUrl))
                {
                    client.BaseAddress = new Uri(baseUrl);
                }
            });

        // Parser + Processing service
        services.AddScoped<ILeaseParser, LeaseParser>();
        services.AddScoped<LeaseProcessingService>();
    });

var host = builder.Build();
host.Run();