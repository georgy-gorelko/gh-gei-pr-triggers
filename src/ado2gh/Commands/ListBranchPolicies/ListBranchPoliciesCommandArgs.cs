using OctoshiftCLI.Commands;

namespace OctoshiftCLI.AdoToGithub.Commands.ListBranchPolicies
{
    public class ListBranchPoliciesCommandArgs : CommandArgs
    {
        public string AdoOrg { get; set; }
        [Secret]
        public string AdoPat { get; set; }
        public string TeamProject { get; set; }
        public string Repo { get; set; }
        public string CsvOutput { get; set; }
        public string StatusChecksCsvOutput { get; set; }
    }
}
