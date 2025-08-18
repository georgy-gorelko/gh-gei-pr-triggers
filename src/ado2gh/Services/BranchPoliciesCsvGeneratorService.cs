using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Services
{
    public class BranchPoliciesCsvGeneratorService
    {
        private readonly OctoLogger _log;

        public BranchPoliciesCsvGeneratorService(OctoLogger log)
        {
            _log = log;
        }

        public virtual async Task<string> Generate(IEnumerable<BranchPolicyRecord> policies)
        {
            await Task.CompletedTask; // Make method async for consistency with other generators

            var result = new StringBuilder();

            // CSV Headers
            result.AppendLine("Organization,TeamProject,Repository,PolicyId,PolicyType,PolicyName,Description,IsEnabled,IsBlocking,MigrationConcern");

            foreach (var policy in policies.OrderBy(p => p.Organization).ThenBy(p => p.TeamProject).ThenBy(p => p.Repository))
            {
                var migrationConcern = GetMigrationConcern(policy.PolicyName);

                result.AppendLine($"\"{EscapeCsvValue(policy.Organization)}\"," +
                                $"\"{EscapeCsvValue(policy.TeamProject)}\"," +
                                $"\"{EscapeCsvValue(policy.Repository)}\"," +
                                $"\"{EscapeCsvValue(policy.PolicyId)}\"," +
                                $"\"{EscapeCsvValue(policy.PolicyType)}\"," +
                                $"\"{EscapeCsvValue(policy.PolicyName)}\"," +
                                $"\"{EscapeCsvValue(policy.Description)}\"," +
                                $"{policy.IsEnabled}," +
                                $"{policy.IsBlocking}," +
                                $"\"{EscapeCsvValue(migrationConcern)}\"");
            }

            return result.ToString();
        }

        public virtual async Task<string> GenerateSummary(Dictionary<string, int> policySummary, int totalRepos, int reposWithPolicies, int reposWithoutPolicies, Dictionary<string, int> topPolicies = null)
        {
            await Task.CompletedTask; // Make method async for consistency

            var result = new StringBuilder();

            // Summary CSV Headers
            result.AppendLine("PolicyType,RepositoryCount");

            foreach (var policyType in policySummary.OrderByDescending(p => p.Value))
            {
                result.AppendLine($"\"{EscapeCsvValue(policyType.Key)}\",{policyType.Value}");
            }

            // Add summary statistics
            result.AppendLine();
            result.AppendLine("Summary Statistics");
            result.AppendLine("Metric,Count");
            result.AppendLine($"\"Total repositories analyzed\",{totalRepos}");
            result.AppendLine($"\"Repositories with branch policies\",{reposWithPolicies}");
            result.AppendLine($"\"Repositories without branch policies\",{reposWithoutPolicies}");

            // Add top policies section if provided
            if (topPolicies != null && topPolicies.Any())
            {
                result.AppendLine();
                result.AppendLine("Top Branch Policies");
                result.AppendLine("PolicyType,RepositoryCount,CoveragePercentage");

                foreach (var policy in topPolicies.OrderByDescending(p => p.Value).Take(10))
                {
                    var percentage = reposWithPolicies > 0 ? policy.Value * 100.0 / reposWithPolicies : 0;
                    result.AppendLine($"\"{EscapeCsvValue(policy.Key)}\",{policy.Value},{percentage:F1}");
                }
            }

            return result.ToString();
        }

        private static string GetMigrationConcern(string policyName)
        {
            var concerningPolicies = new[]
            {
                "Path-based branch protection",
                "Work item linking",
                "Build validation",
                "Status check"
            };

            return concerningPolicies.Contains(policyName) ? "HIGH" : "LOW";
        }

        private static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            // Escape double quotes by doubling them
            return value.Replace("\"", "\"\"");
        }
    }

    public class BranchPolicyRecord
    {
        public string Organization { get; set; }
        public string TeamProject { get; set; }
        public string Repository { get; set; }
        public string PolicyId { get; set; }
        public string PolicyType { get; set; }
        public string PolicyName { get; set; }
        public string Description { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsBlocking { get; set; }
    }
}
