using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.PackageDeployment.CrmPackageExtentionBase;
namespace BeerLike.PDX.Tasks
{
    internal class DeclareTeamsColumnSecurityProfiles : IPdxTask
    {
        private readonly IOrganizationService _service;
        private readonly TraceLogger _packageLog;

        public string TaskName => "DeclareTeamsColumnSecurityProfiles";

        internal DeclareTeamsColumnSecurityProfiles(IOrganizationService service, TraceLogger packageLog)
        {
            _service = service;
            _packageLog = packageLog;
        }

        public void Execute(string configFile, string packageAssetsFolder, string currentPackageLocation)
        {
            throw new NotImplementedException();
        }
    }
}

