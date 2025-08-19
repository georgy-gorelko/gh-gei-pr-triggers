using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using Octoshift.Models;
using OctoshiftCLI.AdoToGithub.Commands.ListBranchPolicies;
using OctoshiftCLI.AdoToGithub.Services;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.ListBranchPolicies;

public class ListBranchPoliciesCommandHandlerTests
{
    private const string ADO_ORG = "foo-org";
    private const string TEAM_PROJECT = "foo-project";
    private const string REPO_NAME = "foo-repo";
    private const string REPO_ID = "repo-id-123";

    private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
    private readonly Mock<AdoApi> _mockAdoApi = TestHelpers.CreateMock<AdoApi>();
    private readonly Mock<BranchPoliciesCsvGeneratorService> _mockCsvGeneratorService = TestHelpers.CreateMock<BranchPoliciesCsvGeneratorService>();
    private readonly Mock<StatusChecksCsvGeneratorService> _mockStatusChecksCsvGeneratorService = TestHelpers.CreateMock<StatusChecksCsvGeneratorService>();

    private readonly ListBranchPoliciesCommandHandler _handler;

    public ListBranchPoliciesCommandHandlerTests()
    {
        _handler = new ListBranchPoliciesCommandHandler(_mockOctoLogger.Object, _mockAdoApi.Object, _mockCsvGeneratorService.Object, _mockStatusChecksCsvGeneratorService.Object);
    }

    [Fact]
    public async Task Handle_With_Specific_Repo_Should_Analyze_Only_That_Repo()
    {
        // Arrange
        var args = new ListBranchPoliciesCommandArgs
        {
            AdoOrg = ADO_ORG,
            TeamProject = TEAM_PROJECT,
            Repo = REPO_NAME
        };

        var repos = new List<AdoRepository>
        {
            new() { Id = REPO_ID, Name = REPO_NAME, Size = 1000, IsDisabled = false },
            new() { Id = "another-repo-id", Name = "another-repo", Size = 2000, IsDisabled = false }
        };

        var policies = new List<(string Id, string Type, string Name, string Description, bool IsEnabled, bool IsBlocking, JObject Settings)>
        {
            ("policy-1", "fa4e907d-c16b-4a4c-9dfa-4906e5d171dd", "Minimum number of reviewers", "Min reviewers: 2", true, true, new JObject()),
            ("policy-2", "fd2167ab-b0be-447a-8ec8-39368250530e", "Comment resolution", "All comments must be resolved", true, false, new JObject())
        };

        _mockAdoApi.Setup(m => m.GetRepos(ADO_ORG, TEAM_PROJECT)).ReturnsAsync(repos);
        _mockAdoApi.Setup(m => m.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, REPO_ID)).ReturnsAsync(policies);

        // Act
        await _handler.Handle(args);

        // Assert
        _mockAdoApi.Verify(m => m.GetRepos(ADO_ORG, TEAM_PROJECT), Times.Once);
        _mockAdoApi.Verify(m => m.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, REPO_ID), Times.Once);
        _mockAdoApi.Verify(m => m.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, "another-repo-id"), Times.Never);

        _mockOctoLogger.Verify(m => m.LogInformation("Starting branch policies analysis..."), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation($"Analyzing team project: {TEAM_PROJECT}"), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation($"  Analyzing repository: {REPO_NAME}"), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("    Found 2 unique branch policies"), Times.Once);
    }

    [Fact]
    public async Task Handle_With_Multiple_Team_Projects_Should_Analyze_All()
    {
        // Arrange
        var args = new ListBranchPoliciesCommandArgs
        {
            AdoOrg = ADO_ORG
        };

        var teamProjects = new List<string> { "project1", "project2" };
        var repos = new List<AdoRepository>
        {
            new() { Id = REPO_ID, Name = REPO_NAME, Size = 1000, IsDisabled = false }
        };

        var policies = new List<(string Id, string Type, string Name, string Description, bool IsEnabled, bool IsBlocking, JObject Settings)>
        {
            ("policy-1", "fa4e907d-c16b-4a4c-9dfa-4906e5d171dd", "Minimum number of reviewers", "Min reviewers: 2", true, true, new JObject())
        };

        _mockAdoApi.Setup(m => m.GetTeamProjects(ADO_ORG)).ReturnsAsync(teamProjects);
        _mockAdoApi.Setup(m => m.GetRepos(ADO_ORG, It.IsAny<string>())).ReturnsAsync(repos);
        _mockAdoApi.Setup(m => m.GetBranchPolicies(ADO_ORG, It.IsAny<string>(), REPO_ID)).ReturnsAsync(policies);

        // Act
        await _handler.Handle(args);

        // Assert
        _mockAdoApi.Verify(m => m.GetTeamProjects(ADO_ORG), Times.Once);
        _mockAdoApi.Verify(m => m.GetRepos(ADO_ORG, "project1"), Times.Once);
        _mockAdoApi.Verify(m => m.GetRepos(ADO_ORG, "project2"), Times.Once);
        _mockAdoApi.Verify(m => m.GetBranchPolicies(ADO_ORG, "project1", REPO_ID), Times.Once);
        _mockAdoApi.Verify(m => m.GetBranchPolicies(ADO_ORG, "project2", REPO_ID), Times.Once);

        _mockOctoLogger.Verify(m => m.LogInformation("Analyzing team project: project1"), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("Analyzing team project: project2"), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Generate_Policy_Summary()
    {
        // Arrange
        var args = new ListBranchPoliciesCommandArgs
        {
            AdoOrg = ADO_ORG,
            TeamProject = TEAM_PROJECT,
            Repo = REPO_NAME
        };

        var repos = new List<AdoRepository>
        {
            new() { Id = REPO_ID, Name = REPO_NAME, Size = 1000, IsDisabled = false }
        };

        var policies = new List<(string Id, string Type, string Name, string Description, bool IsEnabled, bool IsBlocking, JObject Settings)>
        {
            ("policy-1", "type1", "Minimum number of reviewers", "Min reviewers: 2", true, true, new JObject()),
            ("policy-2", "type2", "Comment resolution", "All comments must be resolved", true, false, new JObject()),
            ("policy-3", "type3", "Build validation", "Build: CI Pipeline", false, true, new JObject())
        };

        _mockAdoApi.Setup(m => m.GetRepos(ADO_ORG, TEAM_PROJECT)).ReturnsAsync(repos);
        _mockAdoApi.Setup(m => m.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, REPO_ID)).ReturnsAsync(policies);

        // Act
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation("==== POLICY SUMMARY ===="), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("Branch policies grouped by type and count:"), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("Total repositories analyzed: 1"), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("Repositories with branch policies: 1"), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("Repositories without branch policies: 0"), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Identify_Migration_Concerns()
    {
        // Arrange
        var args = new ListBranchPoliciesCommandArgs
        {
            AdoOrg = ADO_ORG,
            TeamProject = TEAM_PROJECT,
            Repo = REPO_NAME
        };

        var repos = new List<AdoRepository>
        {
            new() { Id = REPO_ID, Name = REPO_NAME, Size = 1000, IsDisabled = false }
        };

        var policies = new List<(string Id, string Type, string Name, string Description, bool IsEnabled, bool IsBlocking, JObject Settings)>
        {
            ("policy-1", "type1", "Path-based branch protection", "Protected paths: 2 pattern(s)", true, true, new JObject()),
            ("policy-2", "type2", "Work item linking", "Work items must be linked", true, false, new JObject())
        };

        _mockAdoApi.Setup(m => m.GetRepos(ADO_ORG, TEAM_PROJECT)).ReturnsAsync(repos);
        _mockAdoApi.Setup(m => m.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, REPO_ID)).ReturnsAsync(policies);

        // Act
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation("==== MIGRATION CONCERNS ===="), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("Repositories with policies that may require special attention during GitHub migration:"), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation($"  {TEAM_PROJECT}/{REPO_NAME}: Path-based branch protection, Work item linking"), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Export_To_Csv_When_CsvOutput_Provided()
    {
        // Arrange
        var csvPath = "test-policies.csv";
        var args = new ListBranchPoliciesCommandArgs
        {
            AdoOrg = ADO_ORG,
            TeamProject = TEAM_PROJECT,
            Repo = REPO_NAME,
            CsvOutput = csvPath
        };

        var repos = new List<AdoRepository>
        {
            new() { Id = REPO_ID, Name = REPO_NAME, Size = 1000, IsDisabled = false }
        };

        var policies = new List<(string Id, string Type, string Name, string Description, bool IsEnabled, bool IsBlocking, JObject Settings)>
        {
            ("policy-1", "type1", "Minimum number of reviewers", "Min reviewers: 2", true, true, new JObject())
        };

        var csvContent = "Organization,TeamProject,Repository,PolicyId,PolicyType,PolicyName,Description,IsEnabled,IsBlocking,MigrationConcern\n";
        var summaryCsvContent = "PolicyType,RepositoryCount\n";

        _mockAdoApi.Setup(m => m.GetRepos(ADO_ORG, TEAM_PROJECT)).ReturnsAsync(repos);
        _mockAdoApi.Setup(m => m.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, REPO_ID)).ReturnsAsync(policies);
        _mockCsvGeneratorService.Setup(m => m.Generate(It.IsAny<IEnumerable<BranchPolicyRecord>>())).ReturnsAsync(csvContent);
        _mockCsvGeneratorService.Setup(m => m.GenerateSummary(It.IsAny<Dictionary<string, int>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, int>>())).ReturnsAsync(summaryCsvContent);

        var writeToFileCalls = new List<(string path, string content)>();
        var handler = new ListBranchPoliciesCommandHandler(_mockOctoLogger.Object, _mockAdoApi.Object, _mockCsvGeneratorService.Object, _mockStatusChecksCsvGeneratorService.Object)
        {
            WriteToFile = (path, content) =>
            {
                writeToFileCalls.Add((path, content));
                return Task.CompletedTask;
            }
        };

        // Act
        await handler.Handle(args);

        // Assert
        _mockCsvGeneratorService.Verify(m => m.Generate(It.IsAny<IEnumerable<BranchPolicyRecord>>()), Times.Once);
        _mockCsvGeneratorService.Verify(m => m.GenerateSummary(It.IsAny<Dictionary<string, int>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, int>>()), Times.Once);

        writeToFileCalls.Should().HaveCount(2);
        writeToFileCalls[0].path.Should().Be(csvPath);
        writeToFileCalls[0].content.Should().Be(csvContent);
        writeToFileCalls[1].path.Should().Be("test-policies-summary.csv");
        writeToFileCalls[1].content.Should().Be(summaryCsvContent);

        _mockOctoLogger.Verify(m => m.LogInformation("Exporting results to CSV..."), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation($"Branch policies data exported to: {csvPath}"), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("Summary data exported to: test-policies-summary.csv"), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Deduplicate_Policies_And_Show_Top_Policies()
    {
        // Arrange
        var args = new ListBranchPoliciesCommandArgs
        {
            AdoOrg = ADO_ORG,
            TeamProject = TEAM_PROJECT,
            Repo = REPO_NAME
        };

        var repos = new List<AdoRepository>
        {
            new() { Id = REPO_ID, Name = REPO_NAME, Size = 1000, IsDisabled = false }
        };

        // Simulate duplicate policies (same Id)
        var policies = new List<(string Id, string Type, string Name, string Description, bool IsEnabled, bool IsBlocking, JObject Settings)>
        {
            ("policy-1", "type1", "Minimum number of reviewers", "Min reviewers: 2", true, true, new JObject()),
            ("policy-1", "type1", "Minimum number of reviewers", "Min reviewers: 2", true, true, new JObject()), // Duplicate
            ("policy-2", "type2", "Comment resolution", "All comments must be resolved", true, false, new JObject())
        };

        _mockAdoApi.Setup(m => m.GetRepos(ADO_ORG, TEAM_PROJECT)).ReturnsAsync(repos);
        _mockAdoApi.Setup(m => m.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, REPO_ID)).ReturnsAsync(policies);

        // Act
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation("    Found 2 unique branch policies"), Times.Once); // Should be 2, not 3
        _mockOctoLogger.Verify(m => m.LogInformation("==== TOP BRANCH POLICIES ===="), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("Most frequently used branch policies across all repositories:"), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Show_No_Policies_Message_When_Empty()
    {
        // Arrange
        var args = new ListBranchPoliciesCommandArgs
        {
            AdoOrg = ADO_ORG,
            TeamProject = TEAM_PROJECT,
            Repo = REPO_NAME
        };

        var repos = new List<AdoRepository>
        {
            new() { Id = REPO_ID, Name = REPO_NAME, Size = 1000, IsDisabled = false }
        };

        var policies = new List<(string Id, string Type, string Name, string Description, bool IsEnabled, bool IsBlocking, JObject Settings)>();

        _mockAdoApi.Setup(m => m.GetRepos(ADO_ORG, TEAM_PROJECT)).ReturnsAsync(repos);
        _mockAdoApi.Setup(m => m.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, REPO_ID)).ReturnsAsync(policies);

        // Act
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation("    Found 0 unique branch policies"), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("No branch policies found across all repositories."), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Skip_Disabled_Repositories()
    {
        // Arrange
        var args = new ListBranchPoliciesCommandArgs
        {
            AdoOrg = ADO_ORG,
            TeamProject = TEAM_PROJECT
        };

        var repos = new List<AdoRepository>
        {
            new() { Id = REPO_ID, Name = REPO_NAME, Size = 1000, IsDisabled = false },
            new() { Id = "disabled-repo-id", Name = "disabled-repo", Size = 2000, IsDisabled = true }
        };

        var policies = new List<(string Id, string Type, string Name, string Description, bool IsEnabled, bool IsBlocking, JObject Settings)>
        {
            ("policy1", "type1", "Policy 1", "Description 1", true, true, new JObject())
        };

        _mockAdoApi.Setup(x => x.GetTeamProjects(ADO_ORG)).ReturnsAsync(new[] { TEAM_PROJECT });
        _mockAdoApi.Setup(x => x.GetRepos(ADO_ORG, TEAM_PROJECT)).ReturnsAsync(repos);
        _mockAdoApi.Setup(x => x.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, REPO_ID)).ReturnsAsync(policies);

        // Act
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation("  Skipping 1 disabled repository(ies): disabled-repo"), Times.Once);
        _mockAdoApi.Verify(x => x.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, "disabled-repo-id"), Times.Never);
        _mockAdoApi.Verify(x => x.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, REPO_ID), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Warn_When_Specific_Repository_Is_Disabled()
    {
        // Arrange
        var args = new ListBranchPoliciesCommandArgs
        {
            AdoOrg = ADO_ORG,
            TeamProject = TEAM_PROJECT,
            Repo = "disabled-repo"
        };

        var repos = new List<AdoRepository>
        {
            new() { Id = "disabled-repo-id", Name = "disabled-repo", Size = 2000, IsDisabled = true }
        };

        _mockAdoApi.Setup(x => x.GetTeamProjects(ADO_ORG)).ReturnsAsync(new[] { TEAM_PROJECT });
        _mockAdoApi.Setup(x => x.GetRepos(ADO_ORG, TEAM_PROJECT)).ReturnsAsync(repos);

        // Act
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogWarning("  Repository 'disabled-repo' is disabled. Skipping branch policy analysis."), Times.Once);
        _mockAdoApi.Verify(x => x.GetBranchPolicies(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Export_Status_Checks_When_StatusChecksCsvOutput_Is_Provided()
    {
        // Arrange
        var args = new ListBranchPoliciesCommandArgs
        {
            AdoOrg = ADO_ORG,
            TeamProject = TEAM_PROJECT,
            Repo = REPO_NAME,
            StatusChecksCsvOutput = "status-checks.csv"
        };

        var repos = new List<AdoRepository>
        {
            new() { Id = REPO_ID, Name = REPO_NAME, Size = 1000, IsDisabled = false }
        };

        var policies = new List<(string Id, string Type, string Name, string Description, bool IsEnabled, bool IsBlocking, JObject Settings)>
        {
            ("policy-1", "status-check-type", "Status check", "Build validation: CI Pipeline", true, true, new JObject()),
            ("policy-2", "reviewer-type", "Minimum number of reviewers", "Min reviewers: 2", true, false, new JObject())
        };

        _mockAdoApi.Setup(m => m.GetRepos(ADO_ORG, TEAM_PROJECT)).ReturnsAsync(repos);
        _mockAdoApi.Setup(m => m.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, REPO_ID)).ReturnsAsync(policies);

        var statusChecksCsvContent = "Repository,TeamProject,StatusName,StatusGenre,IsEnabled,IsBlocking,MigrationConcern\n";
        var statusChecksSummaryCsvContent = "StatusGenre,Count,MigrationConcern\n";

        _mockStatusChecksCsvGeneratorService.Setup(m => m.Generate(It.IsAny<IEnumerable<StatusCheckRecord>>())).ReturnsAsync(statusChecksCsvContent);
        _mockStatusChecksCsvGeneratorService.Setup(m => m.GenerateSummary(It.IsAny<Dictionary<string, int>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, int>>())).ReturnsAsync(statusChecksSummaryCsvContent);

        var writeToFileCalls = new List<(string path, string content)>();
        var handler = new ListBranchPoliciesCommandHandler(_mockOctoLogger.Object, _mockAdoApi.Object, _mockCsvGeneratorService.Object, _mockStatusChecksCsvGeneratorService.Object)
        {
            WriteToFile = (path, content) =>
            {
                writeToFileCalls.Add((path, content));
                return Task.CompletedTask;
            }
        };

        // Act
        await handler.Handle(args);

        // Assert
        _mockStatusChecksCsvGeneratorService.Verify(m => m.Generate(It.IsAny<IEnumerable<StatusCheckRecord>>()), Times.Once);
        _mockStatusChecksCsvGeneratorService.Verify(m => m.GenerateSummary(It.IsAny<Dictionary<string, int>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, int>>()), Times.Once);

        writeToFileCalls.Should().Contain(call => call.path == "status-checks.csv");
        writeToFileCalls.Should().Contain(call => call.path == "status-checks-summary.csv");

        _mockOctoLogger.Verify(m => m.LogInformation("Exporting status checks to CSV..."), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("Status checks data exported to: status-checks.csv"), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("Status checks summary data exported to: status-checks-summary.csv"), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Separate_Status_Checks_From_Other_Policies()
    {
        // Arrange
        var args = new ListBranchPoliciesCommandArgs
        {
            AdoOrg = ADO_ORG,
            TeamProject = TEAM_PROJECT,
            Repo = REPO_NAME,
            StatusChecksCsvOutput = "test-status-checks.csv"
        };

        var repos = new List<AdoRepository>
        {
            new() { Id = REPO_ID, Name = REPO_NAME, Size = 1000, IsDisabled = false }
        };

        var policies = new List<(string Id, string Type, string Name, string Description, bool IsEnabled, bool IsBlocking, JObject Settings)>
        {
            ("policy-1", "status-check-type", "Status check", "Build validation: CI Pipeline", true, true, new JObject()),
            ("policy-2", "status-check-type", "Status check", "Test validation: Unit Tests", true, false, new JObject()),
            ("policy-3", "fa4e907d-c16b-4a4c-9dfa-4906e5d171dd", "Minimum number of reviewers", "Min reviewers: 2", true, true, new JObject()),
            ("policy-4", "fd2167ab-b0be-447a-8ec8-39368250530e", "Comment resolution", "All comments must be resolved", true, false, new JObject())
        };

        _mockAdoApi.Setup(m => m.GetRepos(ADO_ORG, TEAM_PROJECT)).ReturnsAsync(repos);
        _mockAdoApi.Setup(m => m.GetBranchPolicies(ADO_ORG, TEAM_PROJECT, REPO_ID)).ReturnsAsync(policies);

        // Act
        await _handler.Handle(args);

        // Assert
        _mockOctoLogger.Verify(m => m.LogInformation("Exporting status checks to CSV..."), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("Status checks data exported to: test-status-checks.csv"), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("==== POLICY SUMMARY ===="), Times.Once);
        _mockOctoLogger.Verify(m => m.LogInformation("Branch policies grouped by type and count:"), Times.Once);
    }
}
