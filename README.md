# GitHub Enterprise Importer CLI

[![Actions Status: CI](https://github.com/github/gh-gei/workflows/CI/badge.svg)](https://github.com/github/gh-gei/actions?query=workflow%3ACI)


The [GitHub Enterprise Importer](https://docs.github.com/en/migrations/using-github-enterprise-importer) (GEI, formerly Octoshift) is a highly customizable API-first migration offering designed to help you move your enterprise to GitHub Enterprise Cloud. The GEI-CLI wraps the GEI APIs as a cross-platform console application to simplify customizing your migration experience.

> GEI is generally available for repository migrations originating from Azure DevOps or GitHub that target GitHub Enterprise Cloud. It is in public beta for repository migrations from BitBucket Server and Data Center to GitHub Enterprise Cloud.

## Using the GEI CLI
There are 3 separate CLIs that we ship as extensions for the official [GitHub CLI](https://github.com/cli/cli#installation):
- `gh gei` - Run migrations between GitHub products
- `gh ado2gh` - Run migrations from Azure DevOps to GitHub
- `gh bbs2gh` - Run migrations from BitBucket Server or Data Center to GitHub

To use `gh gei` first install the latest [GitHub CLI](https://github.com/cli/cli#installation), then run the command
>`gh extension install github/gh-gei`

To use `gh ado2gh` first install the latest [GitHub CLI](https://github.com/cli/cli#installation), then run the command
>`gh extension install github/gh-ado2gh`

To use `gh bbs2gh` first install the latest [GitHub CLI](https://github.com/cli/cli#installation), then run the command
>`gh extension install github/gh-bbs2gh`

We update the extensions frequently, so make sure you update them on a regular basis:
>`gh extension upgrade github/gh-gei`

To see the available commands and options run:

>`gh gei --help`

>`gh ado2gh --help`

>`gh bbs2gh --help`

### GitHub to GitHub Usage (GitHub.com -> GitHub.com)
1. Create Personal Access Tokens with access to the source GitHub org, and the target GitHub org (for more details on scopes needed refer to our [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer)).

2. Set the GH_SOURCE_PAT and GH_PAT environment variables.

3. Run the `generate-script` command to generate a migration PowerShell script.
>`gh gei generate-script --github-source-org ORGNAME --github-target-org ORGNAME`

4. The previous command will have created a `migrate.ps1` script. Review the steps in the generated script and tweak if necessary.

5. The migrate.ps1 script requires PowerShell to run. If not already installed see the [install instructions](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) to install PowerShell on Windows, Linux, or Mac. Then run the script.

Refer to the [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer) for more details, including differences when migrating from GitHub Enterprise Server.

### Azure DevOps to GitHub Usage
1. Create Personal Access Tokens with access to the Azure DevOps org, and the GitHub org (for more details on scopes needed refer to our [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer)).

2. Set the `ADO_PAT` and `GH_PAT` environment variables.

3. Run the `generate-script` command to generate a migration script.
>`gh ado2gh generate-script --ado-org ORGNAME --github-org ORGNAME --all`

4. The previous command will have created a `migrate.ps1` PowerShell script. Review the steps in the generated script and tweak if necessary.

5. The `migrate.ps1` script requires PowerShell to run. If not already installed see the [install instructions](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) to install PowerShell on Windows, Linux, or Mac. Then run the script.

Refer to the [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer/migrating-repositories-with-github-enterprise-importer/migrating-repositories-from-azure-devops-to-github-enterprise-cloud) for more details.

#### Pre-Migration: Analyzing Branch Policies
Before running `gh ado2gh migrate-repo`, it's recommended to analyze existing Azure DevOps branch policies to identify potential migration concerns:

>`gh ado2gh list-branch-policies --ado-org ORGNAME`

This command will:
- List all branch policies for repositories in your Azure DevOps organization
- Provide a summary report grouped by policy type and count
- Highlight policies that may require special attention during GitHub migration (e.g., Path-based branch protection, Work item linking, Build validation, Status checks)

You can also scope the analysis to specific team projects or repositories:
>`gh ado2gh list-branch-policies --ado-org ORGNAME --team-project PROJECTNAME --repo REPONAME`

**CSV Export**: Export the results to CSV files for further analysis and reporting:
>`gh ado2gh list-branch-policies --ado-org ORGNAME --csv-output branch-policies.csv`

This will generate two files:
- `branch-policies.csv` - Detailed list of all branch policies with migration concern indicators
- `branch-policies-summary.csv` - Summary statistics and policy counts

Some Azure DevOps branch policies don't have direct GitHub equivalents and may block PRs if status checks are enabled for them. This analysis helps you plan your migration strategy and prepare for policy reconfiguration in GitHub.

### Bitbucket Server and Data Center to GitHub Usage
1. Create Personal Access Token for the target GitHub org (for more details on scopes needed refer to our [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer/preparing-to-migrate-with-github-enterprise-importer/managing-access-for-github-enterprise-importer)).

2. Set the `GH_PAT`, `BBS_USERNAME`, and `BBS_PASSWORD` environment variables.

3. If your Bitbucket Server or Data Center instance runs on Windows, set the `SMB_PASSWORD` environment variable.

4. Run the `generate-script` command to generate a migration script.
```
> gh bbs2gh generate-script --bbs-server-url BBS-SERVER-URL \
  --github-org DESTINATION \
  --output FILENAME \
  # Use the following options if your Bitbucket Server instance runs on Linux
  --ssh-user SSH-USER --ssh-private-key PATH-TO-KEY
  # Use the following options if your Bitbucket Server instance runs on Windows
  --smb-user SMB-USER
  # Use the following option if you are running a Bitbucket Data Center cluster or your Bitbucket Server is behind a load balancer
  --archive-download-host ARCHIVE-DOWNLOAD-HOST
```

5. The previous command will have created a `migrate.ps1` PowerShell script. Review the steps in the generated script and tweak if necessary.

6. The `migrate.ps1` script requires PowerShell to run. If not already installed see the [install instructions](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) to install PowerShell on Windows, Linux, or Mac. Then run the script.

Refer to the [official documentation](https://docs.github.com/en/migrations/using-github-enterprise-importer/migrating-repositories-with-github-enterprise-importer/migrating-repositories-from-bitbucket-server-to-github-enterprise-cloud) for more details.

### Skipping version checks

When the CLI is launched, it logs if a newer version of the CLI is available. You can skip this check by setting the `GEI_SKIP_VERSION_CHECK` environment variable to `true`. 

### Skipping GitHub status checks

When the CLI is launched, it logs a warning if there are any ongoing [GitHub incidents](https://www.githubstatus.com/) that might affect your use of the CLI. You can skip this check by setting the `GEI_SKIP_STATUS_CHECK` environment variable to `true`. 

## Contributions

See [Contributing](CONTRIBUTING.md) for more info on how to get involved.
