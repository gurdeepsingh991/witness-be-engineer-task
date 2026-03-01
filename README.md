# Lease Solution - Multi-Layered Architecture Documentation

## Overview

The Lease Solution is a distributed system designed to process and parse lease documents from the HMLR (HM Land Registry) using an asynchronous, event-driven architecture. The system retrieves lease schedules, parses them intelligently, and stores the results in a persistent database for fast retrieval.

## System Architecture

The solution follows a **multi-layered architecture pattern** with clear separation of concerns across four main projects:

### Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                     API Layer (Presentation)                 │
│                      (LeaseApi)                              │
│                                                              │
│  - RESTful HTTP Endpoints                                   │
│  - Request Validation & Routing                             │
│  - Swagger/OpenAPI Documentation                            │
│  - Centralized Exception Handling                           │
│  - Correlation ID Tracing                                   │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                Service/Orchestration Layer                   │
│                      (Services)                              │
│                                                              │
│  - LeaseOrchestrator: Orchestrates the entire flow         │
│  - LeaseProcessingTrigger: Invokes async processing        │
│  - Business Logic & Workflow Management                     │
│  - Idempotent Job Creation & Status Management             │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│                   Domain Layer (Business Logic)              │
│                    (Lease.Domain)                            │
│                                                              │
│  - ILeaseParser: Parser abstraction & interface            │
│  - LeaseParser:  Parsing logic implementation      │
│  - Domain Models: ParsedScheduleNoticeOfLease,             │
│    RawScheduleNoticeOfLease                               │
│  - Enums: JobStatus (Pending, Processing, Completed, etc) │
│  - Pure business rules, no dependencies on infrastructure  │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│              Persistence Layer (Data Access)                 │
│                 (Lease.Infrastructure)                       │
│                                                              │
│  - LeaseDbContext: EF Core DbContext                        │
│  - JobEntity: Job tracking entity                           │
│  - LeaseResultEntity: Parsed lease results storage          │
│  - Repositories: Data access patterns                       │
│  - Database: SQLite (local development)                     │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│              External Integration Layer                      │
│                                                              │
│  - HMLR API Client: Fetches lease schedules                │
│  - HmlrClient: Encapsulates external API calls             │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

### 1. **Lease.Domain** (Domain Layer)
Pure business logic with no external dependencies.

- **Enums/JobStatus.cs**: Enum for job states (Pending, Processing, Completed, Failed, NotFound)
- **Models/**:
  - `RawScheduleNoticeOfLease.cs`: Raw data from HMLR API
  - `ParsedScheduleNoticeOfLease.cs`: Structured parsed lease data
- **Parser/**:
  - `ILeaseParser.cs`: Interface defining parsing contract
  - `AILeaseParser.cs`: AI-powered parsing implementation
  - `LeaseParsers.cs`: Parser factory/wrapper

### 2. **Lease.Infrastructure** (Persistence Layer)
Data access and entity framework configuration.

- **Entities/**:
  - `JobEntity.cs`: Tracks lease processing jobs
  - `LeaseResultEntity.cs`: Stores parsed lease results
- **Persistence/**:
  - `LeaseDbContext.cs`: EF Core DbContext with DbSets for Jobs and LeaseResults

### 3. **LeaseApi** (Presentation/API Layer)
REST API endpoint and business orchestration.

- **Endpoints/LeaseEndpoints.cs**: Maps `GET /{titleNumber}` endpoint
- **Services/**:
  - `LeaseOrchestrator.cs`: Core orchestration logic
  - `LeaseProcessingTrigger.cs`: HTTP client to invoke Azure Function
- **Repositories/**:
  - `JobRepository.cs`: Job data access
  - `LeaseResultRepository.cs`: Lease result data access
- **Contracts/**:
  - `LeaseStatusDto.cs`: Response DTO for job status
  - `ParsedScheduleNoticeOfLeaseDto.cs`: Response DTO for parsed results
- **Program.cs**: Dependency injection & middleware configuration

### 4. **LeaseParserFunction** (Azure Function / Background Worker)
Asynchronous processing of lease documents.

- **Functions/LeaseParserFunction.cs**: Azure Function entry point
- **Services/LeaseProcessingService.cs**: Core parsing and persistence logic
- **Program.cs**: Dependency injection configuration

### 5. **Test Projects**
- **Lease.Domain.Tests**: Domain logic unit tests
- **LeaseApi.Tests**: API and service unit tests

## Overall Flow

### Request-Response Workflow

```
Client Request
    ↓
GET /{titleNumber}
    ↓
LeaseApi (Presentation Layer)
    ↓
LeaseOrchestrator.HandleAsync()
    ├─ Step 1: Check cache (LeaseResultRepository)
    │   └─ If found → Return cached result (202 Accepted)
    │
    ├─ Step 2: Check/Create job record (JobRepository)
    │   └─ If new → Job status = Pending
    │
    ├─ Step 3: Update job to Processing status
    │   └─ Increment attempt count
    │
    ├─ Step 4: Trigger async processing (LeaseProcessingTrigger)
    │   └─ HTTP POST to Azure Function with titleNumber & correlationId
    │
    └─ Step 5: Return 202 Accepted with job status
    
    ↓
Azure Function (LeaseParserFunction) - Async
    ↓
LeaseProcessingService.ProcessAsync()
    ├─ Step 1: Fetch job record by titleNumber
    │
    ├─ Step 2: Call HMLR API to fetch lease schedules (HmlrClient)
    │
    ├─ Step 3: Filter schedules by titleNumber (regex matching)
    │   └─ If no matches → Job status = NotFound
    │
    ├─ Step 4: Parse filtered schedules (ILeaseParser)
    │   └─ Uses AI/intelligent parsing
    │
    ├─ Step 5: Validate parsed results
    │   └─ Check for required fields
    │
    ├─ Step 6: Serialize and store results (LeaseResultRepository)
    │   └─ Create LeaseResultEntity with JSON payload
    │
    ├─ Step 7: Update job status to Completed
    │
    └─ Step 8: Save all changes to database
    
    ↓
Database (SQLite)
    └─ JobEntity & LeaseResultEntity tables updated
```

### Subsequent Requests
When the same `titleNumber` is requested again:
- **Cache Hit**: Orchestrator finds result in `LeaseResults` table → Returns 200 OK with cached data immediately
- **No additional HMLR API calls** → Improves performance

## System Design Patterns Implemented

### 1. **Layered Architecture**
- Clear separation of concerns
- Each layer has a single responsibility
- Dependencies flow downward (Presentation → Service → Domain → Persistence)

### 2. **Repository Pattern**
- `JobRepository` and `LeaseResultRepository` abstract data access
- Enables easy testing and switching persistence implementations

### 3. **Service/Orchestrator Pattern**
- `LeaseOrchestrator` coordinates business workflows
- `LeaseProcessingService` handles domain-specific processing logic

### 4. **Dependency Injection (DI)**
- Registered in `Program.cs` for both API and Function
- Enables loose coupling and testability

### 5. **Async/Await with Task-Based Asynchrony**
- Non-blocking operations throughout
- Improves scalability and resource utilization

### 6. **CQRS-like Pattern** (Implicit)
- API reads from cache (LeaseResults) or triggers writes
- Function performs actual processing and persistence
- Separates read and write concerns

### 7. **Idempotency**
- Job creation is idempotent via `JobRepository.CreateIfMissingAsync()`
- Multiple identical requests won't create duplicate jobs

### 8. **Health Checks & Observability**
- `/health` endpoint in API
- Correlation ID tracking across all requests (X-Correlation-ID header)
- Centralized exception handling with ProblemDetails responses

### 9. **Caching Pattern**
- Cache results in database for fast subsequent retrievals
- Reduces external API calls and improves response times

### 10. **Domain-Driven Design (DDD)**
- Domain entities (JobEntity, LeaseResultEntity)
- Value objects and enums (JobStatus)
- Clear domain boundaries

## Database Schema

### Jobs Table
```
Id (GUID, Primary Key)
TitleNumber (String, Index)
Status (Enum: Pending, Processing, Completed, Failed, NotFound)
AttemptCount (Int)
LastError (String, nullable)
CreatedAt (DateTimeOffset)
UpdatedAt (DateTimeOffset)
```

### LeaseResults Table
```
Id (GUID, Primary Key)
TitleNumber (String, Index)
PayloadJson (String) - Serialized ParsedScheduleNoticeOfLease list
CreatedAt (DateTimeOffset)
```

## Getting Started

### Prerequisites
- **.NET 8.0 SDK** or later
- **SQLite** (included with .NET)
- **Azure Functions Core Tools** (optional, for local function development)
- **Visual Studio 2022** or **Visual Studio Code** with C# extension

### Installation & Setup

#### 1. Clone the Repository
```bash
cd ~/Documents/"My Project Repos"/"Lease Solution"
```

#### 2. Restore NuGet Dependencies
```bash
dotnet restore
```

#### 3. Database Configuration (IMPORTANT - Local Development)

The system uses SQLite with a **hardcoded local database path** in `appsettings.json`:

**File: `LeaseApi/appsettings.json`**
```json
{
  "ConnectionStrings": {
    "Default": "Data Source=lease.db"
  }
}
```

**File: `LeaseParserFunction/local.settings.json`**
```json
{
  "ConnectionStrings": {
    "Default": "Data Source=lease.db"
  }
}
```

**⚠️ IMPORTANT FOR YOUR ENVIRONMENT:**
- If you're running on a different machine or want to use a different database location, update these paths
- For **local development**, you may need to:
  - Change `lease.db` to an absolute path if cross-project database sharing is needed
  - Ensure write permissions to the directory where `lease.db` is created

#### Example: Update to Use Absolute Path
```json
// Windows
"Default": "Data Source=C:\\Users\\YourUsername\\Documents\\lease.db"

// macOS/Linux
"Default": "Data Source=/Users/YourUsername/Documents/lease.db"
```

#### 4. Build the Solution
```bash
dotnet build
```

### Running the System

#### Option A: Run API Only (Without Azure Function)

The API will return `202 Accepted` and attempt to trigger the Azure Function. If the function is unavailable, it will return an error.

**In Terminal 1: Start the API**
```bash
cd LeaseApi
dotnet run
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7125
      Now listening on: http://localhost:5124
```

**In Terminal 2: Make Test Requests**
```bash
# Request 1 (first time - will return 202 Accepted)
curl -X GET "http://localhost:5124/AB123456" \
  -H "X-Correlation-ID: my-test-id-123"

# Expected response (202 Accepted):
# {
#   "titleNumber": "AB123456",
#   "status": "Processing"
# }

# Request 2 (subsequent request - if cached, will return 200 OK)
curl -X GET "http://localhost:5124/AB123456" \
  -H "X-Correlation-ID: my-test-id-456"
```

Or use the Swagger UI:
- Navigate to: `http://localhost:5124/swagger`
- Try the `GET /{titleNumber}` endpoint

#### Option B: Run Full System (API + Azure Function)

**In Terminal 1: Start the Azure Function**
```bash
cd LeaseParserFunction
func start
```

Expected output:
```
Azure Functions Core Tools
Version 4.x.xxxx

Worker process started and initialized.

Http Functions:

	LeaseParserFunction: [POST] http://localhost:7071/api/LeaseParserFunction
```

**In Terminal 2: Start the API**
```bash
cd LeaseApi
dotnet run
```

**In Terminal 3: Make Test Requests**
```bash
# First request - will trigger the function asynchronously
curl -X GET "http://localhost:5124/AB123456" \
  -H "X-Correlation-ID: test-id-1"

# Wait a few seconds for the function to process

# Second request - should return cached result (200 OK)
curl -X GET "http://localhost:5124/AB123456" \
  -H "X-Correlation-ID: test-id-2"
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test Lease.Domain.Tests
dotnet test LeaseApi.Tests

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Configuration Files

**API Configuration:**
- `LeaseApi/appsettings.json` - Database connection, logging, HMLR API settings
- `LeaseApi/appsettings.Development.json` - Development-specific overrides
- `LeaseApi/Properties/launchSettings.json` - Debug profile settings

**Function Configuration:**
- `LeaseParserFunction/local.settings.json` - Local development settings
- `LeaseParserFunction/host.json` - Function runtime configuration

**Example Configuration with HMLR API Base URL:**
```json
{
  "ConnectionStrings": {
    "Default": "Data Source=lease.db"
  },
  "Hmlr": {
    "BaseUrl": "https://api.hmlr.example.com"
  }
}
```

## Troubleshooting

### Database Issues
- **"Cannot open database file"**: Update the `Data Source` path in `appsettings.json` to an absolute path with write permissions
- **"Database locked"**: Close all connections and restart the application
- **"Tables don't exist"**: Run `dotnet run` once - the API/Function will auto-create tables via `db.Database.EnsureCreated()`

### Function Not Triggering
- Ensure Azure Functions Core Tools is installed: `func --version`
- Verify function URL in `LeaseProcessingTrigger` class matches the running function endpoint
- Check correlation ID is being propagated correctly via HTTP headers

### Parser Issues
- Verify HMLR API base URL is configured correctly
- Check that lease schedules are being fetched successfully
- Review parsing logic in `AILeaseParser.cs` for any validation errors

## Technology Stack

| Layer | Technology |
|-------|-----------|
| **Presentation** | ASP.NET Core 8.0, Minimal APIs |
| **Business Logic** | .NET 8.0, C# 12 |
| **Data Access** | Entity Framework Core 8.0, SQLite |
| **Background Processing** | Azure Functions |
| **External Integration** | HttpClient |
| **Testing** | xUnit, Moq (potential) |
| **Documentation** | Swagger/OpenAPI |

## Key Features

✅ **Asynchronous Processing** - Non-blocking request handling  
✅ **Caching** - Database-backed result caching  
✅ **Idempotency** - Safe to retry requests  
✅ **Correlation Tracking** - X-Correlation-ID for request tracing  
✅ **Centralized Error Handling** - Consistent ProblemDetails responses  
✅ **Health Checks** - `/health` endpoint for monitoring  
✅ **Swagger Documentation** - Interactive API documentation  
✅ **Separation of Concerns** - Clean layered architecture  

## Future Improvements

- [ ] Add authentication/authorization (OAuth 2.0, JWT)
- [ ] Implement retry policies with exponential backoff (Polly)
- [ ] Add distributed caching (Redis)
- [ ] Implement event sourcing for audit trail
- [ ] Add comprehensive logging (Serilog)
- [ ] Containerize with Docker
- [ ] Deploy to Azure (App Service, Functions)
- [ ] Add rate limiting
- [ ] Implement webhook notifications on job completion

## Support & Contributing

For issues or questions:
1. Check the troubleshooting section above
2. Review the correlation ID in the response headers
3. Check logs in the terminal where the application is running
4. Review the domain models and parsing logic

---

**Last Updated**: March 1, 2026
