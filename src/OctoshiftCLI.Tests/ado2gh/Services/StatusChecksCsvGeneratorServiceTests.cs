using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OctoshiftCLI.AdoToGithub.Services;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Services;

public class StatusChecksCsvGeneratorServiceTests
{
    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly StatusChecksCsvGeneratorService _service;

    public StatusChecksCsvGeneratorServiceTests()
    {
        _service = new StatusChecksCsvGeneratorService(_mockOctoLogger.Object);
    }

    [Fact]
    public async Task Generate_Should_Return_Csv_With_Headers()
    {
        // Arrange
        var records = new List<StatusCheckRecord>();

        // Act
        var result = await _service.Generate(records);

        // Assert
        result.Should().StartWith("Organization,TeamProject,Repository,PolicyId,PolicyType,StatusName,StatusGenre,Description,IsEnabled,IsBlocking,MigrationConcern");
    }

    [Fact]
    public async Task Generate_Should_Return_Csv_With_Status_Check_Data()
    {
        // Arrange
        var records = new List<StatusCheckRecord>
        {
            new()
            {
                Organization = "test-org",
                TeamProject = "test-project", 
                Repository = "test-repo",
                PolicyId = "policy-1",
                PolicyType = "Build validation",
                StatusName = "CI Pipeline",
                StatusGenre = "Build",
                Description = "Build validation: CI Pipeline",
                IsEnabled = true,
                IsBlocking = true
            }
        };

        // Act
        var result = await _service.Generate(records);

        // Assert
        var lines = result.Split('\n').Where(l => !string.IsNullOrEmpty(l)).ToArray();
        lines.Should().HaveCount(2); // Header + 1 data row

        lines[1].Should().Contain("\"test-org\"");
        lines[1].Should().Contain("\"test-project\"");
        lines[1].Should().Contain("\"test-repo\"");
        lines[1].Should().Contain("\"CI Pipeline\"");
        lines[1].Should().Contain("\"Build\"");
        lines[1].Should().Contain("True,True");
    }

    [Fact]
    public async Task Generate_Should_Escape_Csv_Values_With_Commas()
    {
        // Arrange
        var records = new List<StatusCheckRecord>
        {
            new()
            {
                Organization = "test,org",
                TeamProject = "test,project",
                Repository = "test,repo",
                PolicyId = "policy,1",
                PolicyType = "Build,validation",
                StatusName = "CI, Pipeline",
                StatusGenre = "Build",
                Description = "Build validation: CI, Pipeline",
                IsEnabled = true,
                IsBlocking = true
            }
        };

        // Act
        var result = await _service.Generate(records);

        // Assert
        var lines = result.Split('\n').Where(l => !string.IsNullOrEmpty(l)).ToArray();
        lines[1].Should().Contain("\"test,org\"");
        lines[1].Should().Contain("\"test,project\"");
        lines[1].Should().Contain("\"test,repo\"");
        lines[1].Should().Contain("\"CI, Pipeline\"");
    }

    [Fact]
    public async Task GenerateSummary_Should_Return_Summary_Csv_With_Headers()
    {
        // Arrange
        var statusCheckSummary = new Dictionary<string, int>();
        var globalStatusCheckCount = new Dictionary<string, int>();

        // Act
        var result = await _service.GenerateSummary(statusCheckSummary, 10, 5, 5, globalStatusCheckCount);

        // Assert
        result.Should().Contain("## Status Checks Summary");
        result.Should().Contain("StatusName,RepositoryCount");
        result.Should().Contain("## Summary Statistics");
        result.Should().Contain("Metric,Value");
    }

    [Fact]
    public async Task GenerateSummary_Should_Include_Status_Check_Summary_Data()
    {
        // Arrange
        var statusCheckSummary = new Dictionary<string, int>
        {
            { "Build", 5 },
            { "Test", 3 },
            { "Security", 2 }
        };

        var globalStatusCheckCount = new Dictionary<string, int>
        {
            { "Build", 15 },
            { "Test", 8 },
            { "Security", 4 }
        };

        // Act
        var result = await _service.GenerateSummary(statusCheckSummary, 20, 10, 10, globalStatusCheckCount);

        // Assert
        result.Should().Contain("\"Build\",5");
        result.Should().Contain("\"Test\",3");
        result.Should().Contain("\"Security\",2");
    }

    [Fact]
    public async Task GenerateSummary_Should_Include_Repository_Statistics()
    {
        // Arrange
        var statusCheckSummary = new Dictionary<string, int> { { "Build", 5 } };
        var globalStatusCheckCount = new Dictionary<string, int> { { "Build", 15 } };

        // Act
        var result = await _service.GenerateSummary(statusCheckSummary, 25, 15, 10, globalStatusCheckCount);

        // Assert
        result.Should().Contain("\"Total repositories analyzed\",25");
        result.Should().Contain("\"Repositories with status checks\",15");
        result.Should().Contain("\"Repositories without status checks\",10");
    }

    [Fact]
    public async Task GenerateSummary_Should_Include_Top_Status_Checks_When_Provided()
    {
        // Arrange
        var statusCheckSummary = new Dictionary<string, int> { { "Build", 8 } };
        var globalStatusCheckCount = new Dictionary<string, int> { { "Build", 20 } };

        // Act
        var result = await _service.GenerateSummary(statusCheckSummary, 100, 80, 20, globalStatusCheckCount);

        // Assert
        result.Should().Contain("## Top Status Checks");
        result.Should().Contain("StatusName,RepositoryCount,CoveragePercentage");
        result.Should().Contain("\"Build\",20,20.0");
    }
}
