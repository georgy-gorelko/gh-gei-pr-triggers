namespace OctoshiftCLI.AdoToGithub.Services
{
    public class StatusCheckRecord
    {
        public string Organization { get; set; }
        public string TeamProject { get; set; }
        public string Repository { get; set; }
        public string PolicyId { get; set; }
        public string PolicyType { get; set; }
        public string StatusName { get; set; }
        public string StatusGenre { get; set; }
        public string Description { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsBlocking { get; set; }
    }
}
