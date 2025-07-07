using AspNetDebugDashboard.Core.Models;
using AspNetDebugDashboard.Web.Controllers;
using AspNetDebugDashboard.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using AutoFixture;
using AutoFixture.Xunit2;
using Xunit;

namespace AspNetDebugDashboard.Tests;

public class DebugApiControllerTests
{
    private readonly Mock<IDebugStorage> _mockStorage;
    private readonly Mock<IOptions<DebugConfiguration>> _mockOptions;
    private readonly DebugApiController _controller;
    private readonly Fixture _fixture;

    public DebugApiControllerTests()
    {
        _mockStorage = new Mock<IDebugStorage>();
        _mockOptions = new Mock<IOptions<DebugConfiguration>>();
        _fixture = new Fixture();
        
        var config = new DebugConfiguration { IsEnabled = true };
        _mockOptions.Setup(x => x.Value).Returns(config);
        
        _controller = new DebugApiController(_mockStorage.Object, _mockOptions.Object);
    }

    [Fact]
    public async Task GetStats_WhenEnabled_ReturnsStats()
    {
        // Arrange
        var expectedStats = _fixture.Create<DebugStats>();
        _mockStorage.Setup(x => x.GetStatsAsync()).ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetStats();

        // Assert
        result.Should().BeOfType<ActionResult<DebugStats>>();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedStats);
    }

    [Fact]
    public async Task GetStats_WhenDisabled_ReturnsNotFound()
    {
        // Arrange
        var config = new DebugConfiguration { IsEnabled = false };
        _mockOptions.Setup(x => x.Value).Returns(config);
        var controller = new DebugApiController(_mockStorage.Object, _mockOptions.Object);

        // Act
        var result = await controller.GetStats();

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Theory]
    [AutoData]
    public async Task GetRequests_WithFilter_ReturnsPagedResults(DebugFilter filter)
    {
        // Arrange
        var expectedRequests = _fixture.Create<PagedResult<RequestEntry>>();
        _mockStorage.Setup(x => x.GetRequestsAsync(It.IsAny<DebugFilter>()))
                   .ReturnsAsync(expectedRequests);

        // Act
        var result = await _controller.GetRequests(filter);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedRequests);
        _mockStorage.Verify(x => x.GetRequestsAsync(It.IsAny<DebugFilter>()), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task CreateLog_WithValidRequest_ReturnsId(CreateLogRequest request)
    {
        // Arrange
        var expectedId = Guid.NewGuid().ToString();
        _mockStorage.Setup(x => x.StoreLogAsync(It.IsAny<LogEntry>()))
                   .ReturnsAsync(expectedId);

        // Act
        var result = await _controller.CreateLog(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<object>().Subject;
        response.Should().NotBeNull();
        _mockStorage.Verify(x => x.StoreLogAsync(It.IsAny<LogEntry>()), Times.Once);
    }

    [Fact]
    public async Task ClearAll_WhenCalled_CallsStorageClearAll()
    {
        // Arrange
        _mockStorage.Setup(x => x.ClearAllAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ClearAll();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockStorage.Verify(x => x.ClearAllAsync(), Times.Once);
    }

    [Fact]
    public async Task ExportData_WhenAllowedAndEnabled_ReturnsFile()
    {
        // Arrange
        var config = new DebugConfiguration { IsEnabled = true, AllowDataExport = true };
        _mockOptions.Setup(x => x.Value).Returns(config);
        var controller = new DebugApiController(_mockStorage.Object, _mockOptions.Object);

        var mockStats = _fixture.Create<DebugStats>();
        var mockRequests = _fixture.Create<PagedResult<RequestEntry>>();
        var mockQueries = _fixture.Create<PagedResult<SqlQueryEntry>>();
        var mockLogs = _fixture.Create<PagedResult<LogEntry>>();
        var mockExceptions = _fixture.Create<PagedResult<ExceptionEntry>>();

        _mockStorage.Setup(x => x.GetStatsAsync()).ReturnsAsync(mockStats);
        _mockStorage.Setup(x => x.GetRequestsAsync(It.IsAny<DebugFilter>())).ReturnsAsync(mockRequests);
        _mockStorage.Setup(x => x.GetSqlQueriesAsync(It.IsAny<DebugFilter>())).ReturnsAsync(mockQueries);
        _mockStorage.Setup(x => x.GetLogsAsync(It.IsAny<DebugFilter>())).ReturnsAsync(mockLogs);
        _mockStorage.Setup(x => x.GetExceptionsAsync(It.IsAny<DebugFilter>())).ReturnsAsync(mockExceptions);

        // Act
        var result = await controller.ExportData();

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = result.As<FileContentResult>();
        fileResult.ContentType.Should().Be("application/json");
        fileResult.FileDownloadName.Should().StartWith("debug-export-");
    }

    [Theory]
    [InlineData("test query", new string[0])]
    [InlineData("exception", new[] { "exceptions" })]
    [InlineData("request", new[] { "requests", "logs" })]
    public async Task Search_WithValidTerm_ReturnsResults(string term, string[] types)
    {
        // Arrange
        var mockResults = new List<object> { new { Type = "test", Data = "data" } };
        _mockStorage.Setup(x => x.GetRequestsAsync(It.IsAny<DebugFilter>()))
                   .ReturnsAsync(new PagedResult<RequestEntry> { Items = new List<RequestEntry>() });
        _mockStorage.Setup(x => x.GetSqlQueriesAsync(It.IsAny<DebugFilter>()))
                   .ReturnsAsync(new PagedResult<SqlQueryEntry> { Items = new List<SqlQueryEntry>() });
        _mockStorage.Setup(x => x.GetLogsAsync(It.IsAny<DebugFilter>()))
                   .ReturnsAsync(new PagedResult<LogEntry> { Items = new List<LogEntry>() });
        _mockStorage.Setup(x => x.GetExceptionsAsync(It.IsAny<DebugFilter>()))
                   .ReturnsAsync(new PagedResult<ExceptionEntry> { Items = new List<ExceptionEntry>() });

        // Act
        var result = await _controller.Search(term, types);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Search_WithEmptyTerm_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.Search("", null);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPerformanceMetrics_WhenEnabled_ReturnsMetrics()
    {
        // Arrange
        var config = new DebugConfiguration { IsEnabled = true, EnablePerformanceCounters = true };
        _mockOptions.Setup(x => x.Value).Returns(config);
        var controller = new DebugApiController(_mockStorage.Object, _mockOptions.Object);

        var mockStats = _fixture.Create<DebugStats>();
        var mockRequests = new PagedResult<RequestEntry>
        {
            Items = _fixture.CreateMany<RequestEntry>(10).ToList()
        };

        _mockStorage.Setup(x => x.GetStatsAsync()).ReturnsAsync(mockStats);
        _mockStorage.Setup(x => x.GetRequestsAsync(It.IsAny<DebugFilter>())).ReturnsAsync(mockRequests);

        // Act
        var result = await controller.GetPerformanceMetrics();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetHealth_WhenEnabled_ReturnsHealthInfo()
    {
        // Arrange
        var mockHealth = new { Status = "Healthy", Timestamp = DateTime.UtcNow };
        _mockStorage.Setup(x => x.GetHealthAsync()).ReturnsAsync(mockHealth);

        // Act
        var result = await _controller.GetHealth();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }
}
