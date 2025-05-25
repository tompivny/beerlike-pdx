namespace BeerLike.PDX.Tasks
{
    /// <summary>
    /// Defines the contract for a runnable PDX task.
    /// </summary>
    public interface IPdxTask
    {
        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <param name="service">The IOrganizationService instance to interact with Dataverse.</param>
        /// <param name="packageLog">The TraceLogger for logging progress and errors.</param>
        /// <param name="configFile">Path to the configuration file for the task.</param>
        /// <param name="packageAssetsFolder">The root folder for package assets (e.g., PkgAssets).</param>
        /// <param name="currentPackageLocation">The location of the currently executing package.</param>
        /// <remarks>
        /// We pass service and logger on each execute call to ensure tasks can be stateless if desired,
        /// or they can be stateful and store these if constructed per execution scope.
        /// For now, let's assume tasks might be instantiated once and Execute called.
        /// Alternatively, they could be passed in constructor. Let's adjust based on DeclareTeamsRoles refactoring.
        /// For simplicity in Execute, let's make tasks take service/logger in constructor.
        /// Then Execute only needs task-specific params like configFile.
        /// </remarks>
        // void Execute(IOrganizationService service, TraceLogger packageLog, string configFile, string packageAssetsFolder, string currentPackageLocation);

        /// <summary>
        /// Executes the task using the IOrganizationService and TraceLogger provided during construction.
        /// </summary>
        /// <param name="configFile">Path to the configuration file for the task.</param>
        /// <param name="packageAssetsFolder">The root folder for package assets (e.g., PkgAssets). This helps locate related files like schemas if they are deployed with the package.</param>
        /// <param name="currentPackageLocation">The location of the currently executing package. This can be combined with packageAssetsFolder to resolve full paths.</param>
        void Execute(string configFile, string packageAssetsFolder, string currentPackageLocation);

        /// <summary>
        /// Gets the name of the task, used for logging or identification.
        /// </summary>
        string TaskName { get; }
    }
} 