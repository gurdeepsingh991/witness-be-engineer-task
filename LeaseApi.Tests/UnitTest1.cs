using LeaseApi.Services;
using LeaseApi.Repositories;
using Lease.Infrastructure.Persistence;
using Lease.Infrastructure.Entities;
using Lease.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Moq;

namespace LeaseApi.Tests;

public class LeaseOrchestratorTests
{
    private readonly LeaseDbContext _dbContext;
    private readonly JobRepository _jobRepository;
    private readonly LeaseResultRepository _resultRepository;
    private readonly Mock<LeaseProcessingTrigger> _mockTrigger;
    private readonly Mock<ILogger<LeaseOrchestrator>> _mockLogger;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly LeaseOrchestrator _orchestrator;

    public LeaseOrchestratorTests()
    {
        // Use in-memory database for testing
        var options = new DbContextOptionsBuilder<LeaseDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new LeaseDbContext(options);
        _jobRepository = new JobRepository(_dbContext);
        _resultRepository = new LeaseResultRepository(_dbContext);
        _mockTrigger = new Mock<LeaseProcessingTrigger>(
            new HttpClient(),
            new ConfigurationBuilder().Build());
        _mockLogger = new Mock<ILogger<LeaseOrchestrator>>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        _orchestrator = new LeaseOrchestrator(
            _jobRepository,
            _resultRepository,
            _mockTrigger.Object,
            _mockLogger.Object,
            _mockHttpContextAccessor.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidTitleNumber_ReturnsCachedResultWhenExists()
    {
        // Arrange
        var titleNumber = "TGL24029";
        var payload = "{\"entryNumber\":1,\"propertyDescription\":\"Test Property\"}";
        
        _dbContext.LeaseResults.Add(new LeaseResultEntity
        {
            Id = Guid.NewGuid(),
            TitleNumber = titleNumber,
            PayloadJson = payload,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _orchestrator.HandleAsync(titleNumber, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<object>;
        Assert.NotNull(okResult);
    }

    [Fact]
    public async Task HandleAsync_WithNewTitle_CreatesJobAndTriggersProcessing()
    {
        // Arrange
        var titleNumber = "ABC1234";
        SetupHttpContext();

        // Act
        var result = await _orchestrator.HandleAsync(titleNumber, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var job = await _jobRepository.GetByTitleAsync(titleNumber, CancellationToken.None);
        Assert.NotNull(job);
        Assert.Equal(JobStatus.Processing, job.Status);
        Assert.Equal(1, job.AttemptCount);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidTitleNumber_ReturnsBadRequest()
    {
        // Arrange
        var invalidTitle = "INVALID123";

        // Act
        var result = await _orchestrator.HandleAsync(invalidTitle, CancellationToken.None);

        // Assert
        var badRequest = result as Microsoft.AspNetCore.Http.HttpResults.BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>;
        Assert.NotNull(badRequest);
    }

    [Fact]
    public async Task HandleAsync_WithWhitespaceTitle_TrimAndProcess()
    {
        // Arrange
        var titleNumber = "  TGL24029  ";
        SetupHttpContext();

        // Act
        var result = await _orchestrator.HandleAsync(titleNumber, CancellationToken.None);

        // Assert
        var job = await _jobRepository.GetByTitleAsync("TGL24029", CancellationToken.None);
        Assert.NotNull(job);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyTitle_ReturnsBadRequest()
    {
        // Arrange
        var emptyTitle = "";

        // Act
        var result = await _orchestrator.HandleAsync(emptyTitle, CancellationToken.None);

        // Assert
        var badRequest = result as Microsoft.AspNetCore.Http.HttpResults.BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>;
        Assert.NotNull(badRequest);
    }

    [Fact]
    public async Task HandleAsync_WithFailedJob_ReturnsErrorResponse()
    {
        // Arrange
        var titleNumber = "TGL24029";
        _dbContext.Jobs.Add(new JobEntity
        {
            Id = Guid.NewGuid(),
            TitleNumber = titleNumber,
            Status = JobStatus.Failed,
            AttemptCount = 3,
            LastError = "Processing timeout",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _orchestrator.HandleAsync(titleNumber, CancellationToken.None);

        // Assert
        var problemResult = result as Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult;
        Assert.NotNull(problemResult);
    }

    [Fact]
    public async Task HandleAsync_WithProcessingJob_ReturnsAccepted()
    {
        // Arrange
        var titleNumber = "EGL557357";
        _dbContext.Jobs.Add(new JobEntity
        {
            Id = Guid.NewGuid(),
            TitleNumber = titleNumber,
            Status = JobStatus.Processing,
            AttemptCount = 1,
            LastError = null,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _orchestrator.HandleAsync(titleNumber, CancellationToken.None);

        // Assert
        var acceptedResult = result as Microsoft.AspNetCore.Http.HttpResults.Accepted<object>;
        Assert.NotNull(acceptedResult);
    }

    [Fact]
    public async Task HandleAsync_WhenTriggerFails_UpdatesJobStatusToFailed()
    {
        // Arrange
        var titleNumber = "ABC1234";
        SetupHttpContext();
        _mockTrigger.Setup(t => t.TriggerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Trigger failed"));

        // Act
        var result = await _orchestrator.HandleAsync(titleNumber, CancellationToken.None);

        // Assert
        var problemResult = result as Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult;
        Assert.NotNull(problemResult);
        
        var job = await _jobRepository.GetByTitleAsync(titleNumber, CancellationToken.None);
        Assert.NotNull(job);
        Assert.Equal(JobStatus.Failed, job.Status);
    }

    private void SetupHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-ID"] = "test-correlation-id";
        _mockHttpContextAccessor.Setup(h => h.HttpContext).Returns(httpContext);
    }
}

public class JobRepositoryTests
{
    private readonly LeaseDbContext _dbContext;
    private readonly JobRepository _repository;

    public JobRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<LeaseDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new LeaseDbContext(options);
        _repository = new JobRepository(_dbContext);
    }

    [Fact]
    public async Task GetByTitleAsync_WithExistingTitle_ReturnsJob()
    {
        // Arrange
        var titleNumber = "TGL24029";
        var jobId = Guid.NewGuid();
        _dbContext.Jobs.Add(new JobEntity
        {
            Id = jobId,
            TitleNumber = titleNumber,
            Status = JobStatus.Pending,
            AttemptCount = 0,
            LastError = null,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTitleAsync(titleNumber, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(jobId, result.Id);
        Assert.Equal(titleNumber, result.TitleNumber);
    }

    [Fact]
    public async Task GetByTitleAsync_WithNonExistentTitle_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByTitleAsync("NONEXISTENT", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateIfMissingAsync_WithNewTitle_CreatesJob()
    {
        // Arrange
        var titleNumber = "ABC1234";

        // Act
        var result = await _repository.CreateIfMissingAsync(titleNumber, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(titleNumber, result.TitleNumber);
        Assert.Equal(JobStatus.Pending, result.Status);
        Assert.Equal(0, result.AttemptCount);
    }

    [Fact]
    public async Task CreateIfMissingAsync_WithExistingTitle_ReturnsExisting()
    {
        // Arrange
        var titleNumber = "TGL24029";
        var jobId = Guid.NewGuid();
        _dbContext.Jobs.Add(new JobEntity
        {
            Id = jobId,
            TitleNumber = titleNumber,
            Status = JobStatus.Pending,
            AttemptCount = 0,
            LastError = null,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.CreateIfMissingAsync(titleNumber, CancellationToken.None);

        // Assert
        Assert.Equal(jobId, result.Id);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesJobStatus()
    {
        // Arrange
        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            TitleNumber = "ABC1234",
            Status = JobStatus.Pending,
            AttemptCount = 0,
            LastError = null,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        // Act
        job.Status = JobStatus.Processing;
        job.AttemptCount = 1;
        await _repository.UpdateAsync(job, CancellationToken.None);

        // Assert
        var updated = await _repository.GetByTitleAsync("ABC1234", CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(JobStatus.Processing, updated.Status);
        Assert.Equal(1, updated.AttemptCount);
    }
}

public class LeaseResultRepositoryTests
{
    private readonly LeaseDbContext _dbContext;
    private readonly LeaseResultRepository _repository;

    public LeaseResultRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<LeaseDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new LeaseDbContext(options);
        _repository = new LeaseResultRepository(_dbContext);
    }

    [Fact]
    public async Task GetByTitleAsync_WithExistingResult_ReturnsEntity()
    {
        // Arrange
        var titleNumber = "TGL24029";
        var resultId = Guid.NewGuid();
        var payload = "{\"entryNumber\":1}";
        
        _dbContext.LeaseResults.Add(new LeaseResultEntity
        {
            Id = resultId,
            TitleNumber = titleNumber,
            PayloadJson = payload,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTitleAsync(titleNumber, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(resultId, result.Id);
        Assert.Equal(payload, result.PayloadJson);
    }

    [Fact]
    public async Task GetByTitleAsync_WithNonExistentResult_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByTitleAsync("NONEXISTENT", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_StoresResultEntity()
    {
        // Arrange
        var entity = new LeaseResultEntity
        {
            Id = Guid.NewGuid(),
            TitleNumber = "ABC1234",
            PayloadJson = "{\"test\":\"data\"}",
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        await _repository.SaveAsync(entity, CancellationToken.None);

        // Assert
        var saved = await _repository.GetByTitleAsync("ABC1234", CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal(entity.PayloadJson, saved.PayloadJson);
    }
}