using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Services
{
    public class StatusChecksCsvGeneratorService
    {
        private readonly OctoLogger _log;

        public StatusChecksCsvGeneratorService(OctoLogger log)
        {
            _log = log;
        }

        public virtual async Task<string> Generate(IEnumerable<StatusCheckRecord> statusChecks)
        {
            await Task.CompletedTask; // Make method async for consistency with other generators

            var result = new StringBuilder();

            // CSV Headers
            result.AppendLine("Organization,TeamProject,Repository,PolicyId,PolicyType,StatusName,StatusGenre,Description,IsEnabled,IsBlocking,MigrationConcern");

            foreach (var statusCheck in statusChecks.OrderBy(s => s.Organization).ThenBy(s => s.TeamProject).ThenBy(s => s.Repository))
            {
                var migrationConcern = GetMigrationConcern(statusCheck.StatusGenre);

                result.AppendLine($"\"{EscapeCsvValue(statusCheck.Organization)}\"," +
                                $"\"{EscapeCsvValue(statusCheck.TeamProject)}\"," +
                                $"\"{EscapeCsvValue(statusCheck.Repository)}\"," +
                                $"\"{EscapeCsvValue(statusCheck.PolicyId)}\"," +
                                $"\"{EscapeCsvValue(statusCheck.PolicyType)}\"," +
                                $"\"{EscapeCsvValue(statusCheck.StatusName)}\"," +
                                $"\"{EscapeCsvValue(statusCheck.StatusGenre)}\"," +
                                $"\"{EscapeCsvValue(statusCheck.Description)}\"," +
                                $"{statusCheck.IsEnabled}," +
                                $"{statusCheck.IsBlocking}," +
                                $"\"{EscapeCsvValue(migrationConcern)}\"");
            }

            return result.ToString();
        }

        public virtual async Task<string> GenerateSummary(Dictionary<string, int> statusCheckSummary, int totalRepos, int reposWithStatusChecks, int reposWithoutStatusChecks, Dictionary<string, int> topStatusChecks = null)
        {
            await Task.CompletedTask; // Make method async for consistency

            var result = new StringBuilder();

            // Add summary section
            result.AppendLine("## Status Checks Summary");
            result.AppendLine();
            result.AppendLine("StatusName,RepositoryCount");

            foreach (var summary in statusCheckSummary.OrderByDescending(x => x.Value))
            {
                result.AppendLine($"\"{EscapeCsvValue(summary.Key)}\",{summary.Value}");
            }

            result.AppendLine();
            result.AppendLine("## Summary Statistics");
            result.AppendLine();
            result.AppendLine("Metric,Value");
            result.AppendLine($"\"{EscapeCsvValue("Total repositories analyzed")}\",{totalRepos}");
            result.AppendLine($"\"{EscapeCsvValue("Repositories with status checks")}\",{reposWithStatusChecks}");
            result.AppendLine($"\"{EscapeCsvValue("Repositories without status checks")}\",{reposWithoutStatusChecks}");

            // Add top status checks section if provided
            if (topStatusChecks != null && topStatusChecks.Any())
            {
                result.AppendLine();
                result.AppendLine("## Top Status Checks");
                result.AppendLine();
                result.AppendLine("StatusName,RepositoryCount,CoveragePercentage");

                foreach (var statusCheck in topStatusChecks.OrderByDescending(x => x.Value))
                {
                    var coveragePercentage = totalRepos > 0 ? (double)statusCheck.Value / totalRepos * 100 : 0;
                    result.AppendLine($"\"{EscapeCsvValue(statusCheck.Key)}\",{statusCheck.Value},{coveragePercentage:F1}");
                }
            }

            return result.ToString();
        }

        private static string GetMigrationConcern(string statusGenre)
        {
            // Status checks generally have lower migration concerns than other policies
            // External status checks might need special attention
            return statusGenre?.ToUpperInvariant() switch
            {
                "EXTERNAL" => "MEDIUM",
                "SECURITY" => "HIGH",
                _ => "LOW"
            };
        }

        private static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\"", "\"\"");
        }
    }
}
