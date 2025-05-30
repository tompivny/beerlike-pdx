using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Microsoft.Xrm.Tooling.PackageDeployment.CrmPackageExtentionBase;
using System.Diagnostics;
namespace BeerLike.PDX.Services;

public class ConfigSchemaValidator : IConfigSchemaValidator
{
    public bool Validate(string jsonConfig, string schemaContent, TraceLogger packageLog)
    {
        try
        {
            JSchema schema = JSchema.Parse(schemaContent);
            JToken jsonToValidate = JToken.Parse(jsonConfig);

            bool isValid = jsonToValidate.IsValid(schema, out IList<ValidationError> errors);

            if (!isValid)
            {
                packageLog.Log("JSON configuration is not valid against the schema:", TraceEventType.Error);
                foreach (var error in errors)
                {
                    packageLog.Log($"  - Message: {error.Message}, Path: {error.Path}, Line: {error.LineNumber}, Pos: {error.LinePosition}", TraceEventType.Error);
                }
            }
            return isValid;
        }
        catch (Exception ex) // Catch parsing errors or other issues
        {
            packageLog.Log($"Error during JSON schema validation: {ex.Message}", TraceEventType.Error);
            return false;
        }
    }
}
