using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.AdoToGithub.Services;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.AdoToGithub.Commands.ListBranchPolicies
{
    public class ListBranchPoliciesCommand : CommandBase<ListBranchPoliciesCommandArgs, ListBranchPoliciesCommandHandler>
    {
        public ListBranchPoliciesCommand() : base(
                name: "list-branch-policies",
                description: "Lists all Azure DevOps branch policies for repositories. Useful for identifying potential migration blockers." +
                             Environment.NewLine +
                             "Note: Expects ADO_PAT env variable or --ado-pat option to be set.")
        {
            AddOption(AdoOrg);
            AddOption(AdoPat);
            AddOption(TeamProject);
            AddOption(Repo);
            AddOption(CsvOutput);
            AddOption(StatusChecksCsvOutput);
            AddOption(Verbose);
        }

        public Option<string> AdoOrg { get; } = new("--ado-org")
        {
            Description = "Azure DevOps organization. If not provided will iterate over all orgs that ADO_PAT has access to.",
            IsRequired = true
        };
        public Option<string> AdoPat { get; } = new("--ado-pat");
        public Option<string> TeamProject { get; } = new("--team-project")
        {
            Description = "Azure DevOps team project. If not provided will iterate over all team projects in the org."
        };
        public Option<string> Repo { get; } = new("--repo")
        {
            Description = "Repository name. If not provided will iterate over all repositories in the team project(s)."
        };
        public Option<string> CsvOutput { get; } = new("--csv-output")
        {
            Description = "Path to export branch policies data as CSV. If not provided, results will only be displayed in console."
        };
        public Option<string> StatusChecksCsvOutput { get; } = new("--status-checks-csv-output")
        {
            Description = "Path to export status checks data as CSV. If not provided, status checks will be included in the main CSV or console output."
        };
        public Option<bool> Verbose { get; } = new("--verbose");

        public override ListBranchPoliciesCommandHandler BuildHandler(ListBranchPoliciesCommandArgs args, IServiceProvider sp)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (sp is null)
            {
                throw new ArgumentNullException(nameof(sp));
            }

            var log = sp.GetRequiredService<OctoLogger>();
            var adoApiFactory = sp.GetRequiredService<AdoApiFactory>();
            var adoApi = adoApiFactory.Create(args.AdoPat);
            var csvGeneratorService = sp.GetRequiredService<BranchPoliciesCsvGeneratorService>();
            var statusChecksCsvGeneratorService = sp.GetRequiredService<StatusChecksCsvGeneratorService>();

            return new ListBranchPoliciesCommandHandler(log, adoApi, csvGeneratorService, statusChecksCsvGeneratorService);
        }
    }
}
