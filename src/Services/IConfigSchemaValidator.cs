using Microsoft.Xrm.Tooling.PackageDeployment.CrmPackageExtentionBase;

namespace BeerLike.PDX.Services
{
    public interface IConfigSchemaValidator
    {
        bool Validate(string jsonConfig, string schemaContent, TraceLogger packageLog);
    }
}