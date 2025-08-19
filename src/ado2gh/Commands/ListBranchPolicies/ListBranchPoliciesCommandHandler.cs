using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OctoshiftCLI.AdoToGithub.Services;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.ListBranchPolicies;

public class ListBranchPoliciesCommandHandler : ICommandHandler<ListBranchPoliciesCommandArgs>
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

    private readonly OctoLogger _log;
    private readonly AdoApi _adoApi;
    private readonly BranchPoliciesCsvGeneratorService _csvGeneratorService;
    private readonly StatusChecksCsvGeneratorService _statusChecksCsvGeneratorService;

    public ListBranchPoliciesCommandHandler(OctoLogger log, AdoApi adoApi, BranchPoliciesCsvGeneratorService csvGeneratorService, StatusChecksCsvGeneratorService statusChecksCsvGeneratorService)
    {
        _log = log;
        _adoApi = adoApi;
        _csvGeneratorService = csvGeneratorService;
        _statusChecksCsvGeneratorService = statusChecksCsvGeneratorService;
    }

    public async Task Handle(ListBranchPoliciesCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _log.LogInformation("Starting branch policies analysis...");

        var allPolicies = new List<RepositoryPolicyReport>();
        var policySummary = new Dictionary<string, int>();
        var csvRecords = new List<BranchPolicyRecord>();
        var statusCheckRecords = new List<StatusCheckRecord>();
        var statusCheckSummary = new Dictionary<string, int>();
        var globalPolicyCount = new Dictionary<string, int>(); // Track global policy usage for top policies
        var globalStatusCheckCount = new Dictionary<string, int>(); // Track global status check usage

        var teamProjects = string.IsNullOrEmpty(args.TeamProject)
            ? await _adoApi.GetTeamProjects(args.AdoOrg)
            : new[] { args.TeamProject };

        foreach (var teamProject in teamProjects)
        {
            _log.LogInformation($"Analyzing team project: {teamProject}");

            try
            {
                // Get all repositories first to count total and disabled
                var allRepos = await _adoApi.GetRepos(args.AdoOrg, teamProject);
                var allReposList = allRepos.ToList();

                // Handle specific repository request
                if (!string.IsNullOrEmpty(args.Repo))
                {
                    var requestedRepo = allReposList.FirstOrDefault(r => r.Name.Equals(args.Repo, StringComparison.OrdinalIgnoreCase));
                    
                    if (requestedRepo == null)
                    {
                        _log.LogWarning($"  Repository '{args.Repo}' not found in team project '{teamProject}'");
                        continue;
                    }
                    
                    if (requestedRepo.IsDisabled)
                    {
                        _log.LogWarning($"  Repository '{args.Repo}' is disabled. Skipping branch policy analysis.");
                        continue;
                    }
                }

                // Filter to only active (enabled) repositories
                var repos = string.IsNullOrEmpty(args.Repo)
                    ? allReposList.Where(r => !r.IsDisabled)
                    : allReposList.Where(r => r.Name.Equals(args.Repo, StringComparison.OrdinalIgnoreCase) && !r.IsDisabled);

                var disabledRepos = allReposList.Where(r => r.IsDisabled).ToList();

                if (disabledRepos.Any() && string.IsNullOrEmpty(args.Repo))
                {
                    _log.LogInformation($"  Skipping {disabledRepos.Count} disabled repository(ies): {string.Join(", ", disabledRepos.Select(r => r.Name))}");
                }

                foreach (var repo in repos)
                {
                    _log.LogInformation($"  Analyzing repository: {repo.Name}");

                    try
                    {
                        var policies = await _adoApi.GetBranchPolicies(args.AdoOrg, teamProject, repo.Id);
                        var policyList = policies.ToList();

                        // Deduplicate policies by PolicyId to ensure uniqueness per repository
                        var uniquePolicies = policyList
                            .GroupBy(p => p.Id)
                            .Select(g => g.First())
                            .ToList();

                        var repoReport = new RepositoryPolicyReport
                        {
                            Organization = args.AdoOrg,
                            TeamProject = teamProject,
                            Repository = repo.Name,
                            Policies = uniquePolicies.Select(p => new PolicyInfo
                            {
                                Id = p.Id,
                                Type = p.Type,
                                Name = p.Name,
                                Description = p.Description,
                                IsEnabled = p.IsEnabled,
                                IsBlocking = p.IsBlocking
                            }).ToList()
                        };

                        allPolicies.Add(repoReport);

                        // Count policies for summary and collect CSV records
                        foreach (var policy in uniquePolicies)
                        {
                            var policyKey = policy.Name;
                            
                            // Check if this is a status check (either by type containing "STATUS" or name being "Status check")
                            if (policy.Type.ToUpperInvariant().Contains("STATUS") || policy.Name.Equals("Status check", StringComparison.OrdinalIgnoreCase))
                            {
                                // Extract status check specific information from settings if available, otherwise from description
                                var statusName = ExtractStatusName(policy.Description, policy.Settings);
                                var statusGenre = ExtractStatusGenre(policy.Description, policy.Settings);

                                // Handle as status check - use the extracted statusName for unique identification
                                statusCheckSummary[statusName] = statusCheckSummary.GetValueOrDefault(statusName, 0) + 1;
                                globalStatusCheckCount[statusName] = globalStatusCheckCount.GetValueOrDefault(statusName, 0) + 1;

                                statusCheckRecords.Add(new StatusCheckRecord
                                {
                                    Organization = args.AdoOrg,
                                    TeamProject = teamProject,
                                    Repository = repo.Name,
                                    PolicyId = policy.Id,
                                    PolicyType = policy.Type,
                                    StatusName = statusName,
                                    StatusGenre = statusGenre,
                                    Description = policy.Description,
                                    IsEnabled = policy.IsEnabled,
                                    IsBlocking = policy.IsBlocking
                                });
                            }
                            else
                            {
                                // Handle as regular branch policy
                                policySummary[policyKey] = policySummary.GetValueOrDefault(policyKey, 0) + 1;
                                globalPolicyCount[policyKey] = globalPolicyCount.GetValueOrDefault(policyKey, 0) + 1;

                                csvRecords.Add(new BranchPolicyRecord
                                {
                                    Organization = args.AdoOrg,
                                    TeamProject = teamProject,
                                    Repository = repo.Name,
                                    PolicyId = policy.Id,
                                    PolicyType = policy.Type,
                                    PolicyName = policy.Name,
                                    Description = policy.Description,
                                    IsEnabled = policy.IsEnabled,
                                    IsBlocking = policy.IsBlocking
                                });
                            }
                        }

                        _log.LogInformation($"    Found {uniquePolicies.Count} unique branch policies");
                    }
                    catch (HttpRequestException ex)
                    {
                        _log.LogWarning($"    Failed to analyze repository {repo.Name}: {ex.Message}");
                    }
                    catch (TaskCanceledException ex)
                    {
                        _log.LogWarning($"    Request timeout analyzing repository {repo.Name}: {ex.Message}");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _log.LogWarning($"Failed to analyze team project {teamProject}: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _log.LogWarning($"Request timeout analyzing team project {teamProject}: {ex.Message}");
            }
        }

        // Display results
        _log.LogInformation("");
        _log.LogInformation("==== BRANCH POLICIES REPORT ====");
        _log.LogInformation("");

        foreach (var repoReport in allPolicies.OrderBy(r => r.TeamProject).ThenBy(r => r.Repository))
        {
            _log.LogInformation($"Organization: {repoReport.Organization}");
            _log.LogInformation($"Team Project: {repoReport.TeamProject}");
            _log.LogInformation($"Repository: {repoReport.Repository}");
            _log.LogInformation($"Branch Policies Count: {repoReport.Policies.Count}");

            if (repoReport.Policies.Any())
            {
                _log.LogInformation("Policies:");
                foreach (var policy in repoReport.Policies)
                {
                    var status = policy.IsEnabled ? "ENABLED" : "DISABLED";
                    var blocking = policy.IsBlocking ? "BLOCKING" : "NON-BLOCKING";
                    _log.LogInformation($"  - {policy.Name} ({status}, {blocking})");
                    if (!string.IsNullOrEmpty(policy.Description))
                    {
                        _log.LogInformation($"    {policy.Description}");
                    }
                }
            }
            else
            {
                _log.LogInformation("  No branch policies configured");
            }

            _log.LogInformation("");
        }

        // Display summary
        _log.LogInformation("==== POLICY SUMMARY ====");
        _log.LogInformation("");
        _log.LogInformation("Branch policies grouped by type and count:");

        foreach (var policyType in policySummary.OrderByDescending(p => p.Value))
        {
            _log.LogInformation($"  {policyType.Key}: {policyType.Value} repositories");
        }

        _log.LogInformation("");
        _log.LogInformation($"Total repositories analyzed: {allPolicies.Count}");
        _log.LogInformation($"Repositories with branch policies: {allPolicies.Count(r => r.Policies.Any())}");
        _log.LogInformation($"Repositories without branch policies: {allPolicies.Count(r => !r.Policies.Any())}");

        // Display top policies summary
        _log.LogInformation("");
        _log.LogInformation("==== TOP BRANCH POLICIES ====");
        _log.LogInformation("");

        var topPolicies = globalPolicyCount
            .OrderByDescending(p => p.Value)
            .Take(10) // Show top 10 policies
            .ToList();

        if (topPolicies.Any())
        {
            _log.LogInformation("Most frequently used branch policies across all repositories:");
            foreach (var policy in topPolicies)
            {
                var percentage = policy.Value * 100.0 / allPolicies.Count(r => r.Policies.Any());
                _log.LogInformation($"  {policy.Key}: {policy.Value} repositories ({percentage:F1}% coverage)");
            }
        }
        else
        {
            _log.LogInformation("No branch policies found across all repositories.");
        }

        // Identify potential GitHub migration concerns
        var concerningPolicies = new[]
        {
            "Path-based branch protection",
            "Work item linking",
            "Build validation",
            "Status check"
        };

        var reposWithConcerns = allPolicies
            .Where(r => r.Policies.Any(p => concerningPolicies.Contains(p.Name)))
            .ToList();

        if (reposWithConcerns.Any())
        {
            _log.LogInformation("");
            _log.LogInformation("==== MIGRATION CONCERNS ====");
            _log.LogInformation("");
            _log.LogInformation("Repositories with policies that may require special attention during GitHub migration:");

            foreach (var repo in reposWithConcerns)
            {
                var concerns = repo.Policies.Where(p => concerningPolicies.Contains(p.Name)).ToList();
                _log.LogInformation($"  {repo.TeamProject}/{repo.Repository}: {string.Join(", ", concerns.Select(c => c.Name))}");
            }
        }

        // Export to CSV if requested
        if (!string.IsNullOrEmpty(args.CsvOutput))
        {
            _log.LogInformation("");
            _log.LogInformation("Exporting results to CSV...");

            try
            {
                var csvContent = await _csvGeneratorService.Generate(csvRecords);
                await WriteToFile(args.CsvOutput, csvContent);

                _log.LogInformation($"Branch policies data exported to: {args.CsvOutput}");

                // Also export summary CSV
                var summaryPath = Path.ChangeExtension(args.CsvOutput, null) + "-summary.csv";
                var summaryCsvContent = await _csvGeneratorService.GenerateSummary(
                    policySummary,
                    allPolicies.Count,
                    allPolicies.Count(r => r.Policies.Any()),
                    allPolicies.Count(r => !r.Policies.Any()),
                    globalPolicyCount
                );
                await WriteToFile(summaryPath, summaryCsvContent);

                _log.LogInformation($"Summary data exported to: {summaryPath}");
            }
            catch (IOException ex)
            {
                _log.LogError($"Failed to export CSV due to file access error: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _log.LogError($"Failed to export CSV due to permission error: {ex.Message}");
            }
        }

        // Export status checks to separate CSV if requested
        if (!string.IsNullOrEmpty(args.StatusChecksCsvOutput))
        {
            _log.LogInformation("");
            _log.LogInformation("Exporting status checks to CSV...");

            try
            {
                var statusChecksCsvContent = await _statusChecksCsvGeneratorService.Generate(statusCheckRecords);
                await WriteToFile(args.StatusChecksCsvOutput, statusChecksCsvContent);

                _log.LogInformation($"Status checks data exported to: {args.StatusChecksCsvOutput}");

                // Also export status checks summary CSV
                var statusChecksSummaryPath = Path.ChangeExtension(args.StatusChecksCsvOutput, null) + "-summary.csv";
                var statusChecksSummaryCsvContent = await _statusChecksCsvGeneratorService.GenerateSummary(
                    statusCheckSummary,
                    allPolicies.Count,
                    allPolicies.Count(r => r.Policies.Any(p => p.Name.Equals("Status check", StringComparison.OrdinalIgnoreCase))),
                    allPolicies.Count(r => !r.Policies.Any(p => p.Name.Equals("Status check", StringComparison.OrdinalIgnoreCase))),
                    globalStatusCheckCount
                );
                await WriteToFile(statusChecksSummaryPath, statusChecksSummaryCsvContent);

                _log.LogInformation($"Status checks summary data exported to: {statusChecksSummaryPath}");
            }
            catch (IOException ex)
            {
                _log.LogError($"Failed to export status checks CSV due to file access error: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _log.LogError($"Failed to export status checks CSV due to permission error: {ex.Message}");
            }
        }

        _log.LogInformation("");
        _log.LogInformation("Branch policies analysis completed.");
    }

    private static string ExtractStatusName(string description, JObject settings)
    {
        // First try to get status name from settings if available
        if (settings != null)
        {
            var statusName = (string)settings["statusName"];
            if (!string.IsNullOrEmpty(statusName))
            {
                return statusName;
            }
        }

        // Fallback to extracting from description like "Status: SonarQube (external)"
        if (!string.IsNullOrEmpty(description) && description.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
        {
            var startIndex = description.IndexOf(":", StringComparison.OrdinalIgnoreCase) + 1;
            var endIndex = description.IndexOf("(", StringComparison.OrdinalIgnoreCase);
            
            var extractedName = endIndex == -1 ? description[startIndex..].Trim() : description[startIndex..endIndex].Trim();
            if (!string.IsNullOrEmpty(extractedName))
            {
                return extractedName;
            }
        }

        // Final fallback - return a generic identifier
        return "Status Check";
    }

    private static string ExtractStatusGenre(string description, JObject settings)
    {
        // First try to get status genre from settings if available
        if (settings != null)
        {
            var statusGenre = (string)settings["statusGenre"];
            if (!string.IsNullOrEmpty(statusGenre))
            {
                return statusGenre;
            }
        }

        // Fallback to extracting from description like "Status: SonarQube (external)"
        if (!string.IsNullOrEmpty(description))
        {
            var startIndex = description.IndexOf("(", StringComparison.OrdinalIgnoreCase);
            var endIndex = description.IndexOf(")", StringComparison.OrdinalIgnoreCase);
            
            if (startIndex != -1 && endIndex != -1)
            {
                var extractedGenre = description.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
                if (!string.IsNullOrEmpty(extractedGenre))
                {
                    return extractedGenre;
                }
            }
        }

        // Final fallback - return a generic genre
        return "Unknown";
    }

    private class RepositoryPolicyReport
    {
        public string Organization { get; set; }
        public string TeamProject { get; set; }
        public string Repository { get; set; }
        public List<PolicyInfo> Policies { get; set; } = [];
    }

    private class PolicyInfo
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsBlocking { get; set; }
    }
}
