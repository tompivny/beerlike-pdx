using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace BeerLike.PDX.Build.Tasks
{
    public class ValidateJsonFile: Microsoft.Build.Utilities.Task
    {
        [Required]
        public ITaskItem JsonFile { get; set; }

        [Required]
        public ITaskItem SchemaFile { get; set; }

        public override bool Execute()
        {
            try
            {
                JSchema schema = JSchema.Parse(SchemaFile.GetMetadata("FullPath"));
                JToken jsonToValidate = JToken.Parse(JsonFile.GetMetadata("FullPath"));

                var isValid = jsonToValidate.IsValid(schema, out IList<ValidationError> errors);

                if (!isValid)
                {
                    foreach (var error in errors)
                    {
                        Log.LogError($"JSON validation error: {error.Message}");
                    }
                }
                return isValid;
            }
            catch (Exception ex) // Catch parsing errors or other issues
            {
                Log.LogError($"Error during JSON schema validation: {ex.Message}");
                return false;
            }

        }
    }
}

