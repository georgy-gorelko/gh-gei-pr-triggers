using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using OctoshiftCLI.AdoToGithub.Services;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Services
{
    public class BranchPoliciesCsvGeneratorServiceTests
    {
        private readonly BranchPoliciesCsvGeneratorService _service;

        public BranchPoliciesCsvGeneratorServiceTests()
        {
            var mockLogger = TestHelpers.CreateMock<OctoLogger>();
            _service = new BranchPoliciesCsvGeneratorService(mockLogger.Object);
        }

        [Fact]
        public async Task Generate_Should_Produce_Valid_Csv()
        {
            // Arrange
            var policies = new List<BranchPolicyRecord>
            {
                new()
                {
                    Organization = "TestOrg",
                    TeamProject = "TestProject",
                    Repository = "TestRepo",
                    PolicyId = "policy-1",
                    PolicyType = "Type1",
                    PolicyName = "Minimum number of reviewers",
                    Description = "Min reviewers: 2",
                    IsEnabled = true,
                    IsBlocking = true
                },
                new()
                {
                    Organization = "TestOrg",
                    TeamProject = "TestProject",
                    Repository = "TestRepo",
                    PolicyId = "policy-2",
                    PolicyType = "Type2",
                    PolicyName = "Work item linking",
                    Description = "Work items must be linked",
                    IsEnabled = false,
                    IsBlocking = false
                }
            };

            // Act
            var result = await _service.Generate(policies);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("Organization,TeamProject,Repository,PolicyId,PolicyType,PolicyName,Description,IsEnabled,IsBlocking,MigrationConcern");
            result.Should().Contain("TestOrg");
            result.Should().Contain("TestProject");
            result.Should().Contain("TestRepo");
            result.Should().Contain("Minimum number of reviewers");
            result.Should().Contain("Work item linking");
            result.Should().Contain("HIGH"); // Work item linking should be marked as high concern
            result.Should().Contain("LOW");  // Minimum reviewers should be marked as low concern
        }

        [Fact]
        public async Task Generate_Should_Handle_Empty_List()
        {
            // Arrange
            var policies = new List<BranchPolicyRecord>();

            // Act
            var result = await _service.Generate(policies);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("Organization,TeamProject,Repository,PolicyId,PolicyType,PolicyName,Description,IsEnabled,IsBlocking,MigrationConcern");
            result.Split('\n').Length.Should().Be(2); // Header + empty line
        }

        [Fact]
        public async Task GenerateSummary_Should_Produce_Valid_Summary_Csv()
        {
            // Arrange
            var policySummary = new Dictionary<string, int>
            {
                { "Minimum number of reviewers", 5 },
                { "Build validation", 3 },
                { "Work item linking", 2 }
            };

            // Act
            var result = await _service.GenerateSummary(policySummary, 10, 8, 2);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("PolicyType,RepositoryCount");
            result.Should().Contain("Minimum number of reviewers");
            result.Should().Contain("Build validation");
            result.Should().Contain("Work item linking");
            result.Should().Contain("Total repositories analyzed");
            result.Should().Contain("10");
            result.Should().Contain("8");
            result.Should().Contain("2");
        }

        [Fact]
        public async Task GenerateSummary_Should_Include_Top_Policies_When_Provided()
        {
            // Arrange
            var policySummary = new Dictionary<string, int>
            {
                { "Minimum number of reviewers", 5 },
                { "Build validation", 3 }
            };
            var topPolicies = new Dictionary<string, int>
            {
                { "Minimum number of reviewers", 5 },
                { "Build validation", 3 },
                { "Comment resolution", 2 }
            };

            // Act
            var result = await _service.GenerateSummary(policySummary, 10, 8, 2, topPolicies);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("PolicyType,RepositoryCount");
            result.Should().Contain("Top Branch Policies");
            result.Should().Contain("PolicyType,RepositoryCount,CoveragePercentage");
            result.Should().Contain("\"Minimum number of reviewers\",5,62.5");
            result.Should().Contain("\"Build validation\",3,37.5");
            result.Should().Contain("\"Comment resolution\",2,25");
        }

        [Fact]
        public async Task Generate_Should_Escape_Csv_Values_Properly()
        {
            // Arrange
            var policies = new List<BranchPolicyRecord>
            {
                new()
                {
                    Organization = "Test\"Org",
                    TeamProject = "Test,Project",
                    Repository = "Test\nRepo",
                    PolicyId = "policy-1",
                    PolicyType = "Type1",
                    PolicyName = "Test Policy",
                    Description = "Description with \"quotes\" and, commas",
                    IsEnabled = true,
                    IsBlocking = false
                }
            };

            // Act
            var result = await _service.Generate(policies);

            // Assert
            result.Should().Contain("Test\"\"Org"); // Escaped quotes
            result.Should().Contain("Test,Project");
            result.Should().Contain("Description with \"\"quotes\"\" and, commas"); // Escaped quotes
        }
    }
}
