using Microsoft.Extensions.DependencyInjection;
using Moq;
using OctoshiftCLI.AdoToGithub.Commands.ListBranchPolicies;
using OctoshiftCLI.AdoToGithub.Factories;
using OctoshiftCLI.AdoToGithub.Services;
using OctoshiftCLI.Services;
using Xunit;

namespace OctoshiftCLI.Tests.AdoToGithub.Commands.ListBranchPolicies
{
    public class ListBranchPoliciesCommandTests
    {
        private readonly Mock<AdoApiFactory> _mockAdoApiFactory = TestHelpers.CreateMock<AdoApiFactory>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        private readonly Mock<BranchPoliciesCsvGeneratorService> _mockCsvGeneratorService = TestHelpers.CreateMock<BranchPoliciesCsvGeneratorService>();
        private readonly Mock<StatusChecksCsvGeneratorService> _mockStatusChecksCsvGeneratorService = TestHelpers.CreateMock<StatusChecksCsvGeneratorService>();

        private readonly ServiceProvider _serviceProvider;
        private readonly ListBranchPoliciesCommand _command = [];

        public ListBranchPoliciesCommandTests()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection
                .AddSingleton(_mockOctoLogger.Object)
                .AddSingleton(_mockAdoApiFactory.Object)
                .AddSingleton(_mockCsvGeneratorService.Object)
                .AddSingleton(_mockStatusChecksCsvGeneratorService.Object);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact]
        public void Should_Have_Options()
        {
            Assert.NotNull(_command);
            Assert.Equal("list-branch-policies", _command.Name);
            Assert.Equal(7, _command.Options.Count);

            TestHelpers.VerifyCommandOption(_command.Options, "ado-org", true);
            TestHelpers.VerifyCommandOption(_command.Options, "ado-pat", false);
            TestHelpers.VerifyCommandOption(_command.Options, "team-project", false);
            TestHelpers.VerifyCommandOption(_command.Options, "repo", false);
            TestHelpers.VerifyCommandOption(_command.Options, "csv-output", false);
            TestHelpers.VerifyCommandOption(_command.Options, "status-checks-csv-output", false);
            TestHelpers.VerifyCommandOption(_command.Options, "verbose", false);
        }

        [Fact]
        public void It_Should_Build_Handler()
        {
            var args = new ListBranchPoliciesCommandArgs
            {
                AdoOrg = "ADO-ORG",
                AdoPat = "ADO-PAT"
            };

            var handler = _command.BuildHandler(args, _serviceProvider);

            Assert.NotNull(handler);
            Assert.IsType<ListBranchPoliciesCommandHandler>(handler);
        }
    }
}
