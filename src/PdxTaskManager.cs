using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.PackageDeployment.CrmPackageExtentionBase;
using System.Text.Json;
using System.Diagnostics;
using BeerLike.PDX.Models;
using BeerLike.PDX.Tasks;

namespace BeerLike.PDX
{
    public class PdxTaskManager
    {
        private readonly IOrganizationService _service;
        private readonly TraceLogger _packageLog;
        private readonly string _packageAssetsFolder;
        private readonly string _currentPackageLocation;
        private readonly Dictionary<string, Type> _registeredTaskTypes;

        public PdxTaskManager(IOrganizationService service, TraceLogger packageLog, string packageAssetsFolder, string currentPackageLocation)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _packageLog = packageLog ?? throw new ArgumentNullException(nameof(packageLog));
            _packageAssetsFolder = packageAssetsFolder ?? throw new ArgumentNullException(nameof(packageAssetsFolder));
            _currentPackageLocation = currentPackageLocation ?? throw new ArgumentNullException(nameof(currentPackageLocation));
            
            _registeredTaskTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            RegisterAvailableTasks();
        }

        private void RegisterAvailableTasks()
        {
            // Manually register known task types from this assembly (BeerLike.PDX)
            // Key is the string that will be used in pdx_tasks.json (should match IPdxTask.TaskName)
            // Value is the Type of the class implementing IPdxTask.

            var declareTeamsRolesTask = new DeclareTeamsRoles(_service, _packageLog); // Temp instance to get TaskName
            _registeredTaskTypes.Add(declareTeamsRolesTask.TaskName, typeof(DeclareTeamsRoles));
            _packageLog.Log($"PdxTaskManager: Registered task type '{declareTeamsRolesTask.TaskName}' -> {typeof(DeclareTeamsRoles).FullName}", TraceEventType.Verbose);

            var declareTeamsColumnSecurityProfiles = new DeclareTeamsColumnSecurityProfiles(_service, _packageLog);
            _registeredTaskTypes.Add(declareTeamsColumnSecurityProfiles.TaskName, typeof(DeclareTeamsColumnSecurityProfiles));
            _packageLog.Log($"PdxTaskManager: Registered task type '{declareTeamsColumnSecurityProfiles.TaskName}' -> {typeof(DeclareTeamsColumnSecurityProfiles).FullName}", TraceEventType.Verbose);
            

            // To add a new task:
            // 1. Create YourNewTask : IPdxTask in BeerLike.PDX.Tasks
            // 2. var yourNewTask = new YourNewTask(_service, _packageLog); // Temp instance for TaskName
            // 3. _registeredTaskTypes.Add(yourNewTask.TaskName, typeof(YourNewTask));
        }

        public void ExecuteTasks(string masterConfigFileName)
        {
            if (string.IsNullOrEmpty(masterConfigFileName))
            {
                _packageLog.Log("Master PDX task configuration file name not provided. No tasks will be executed.", TraceEventType.Warning);
                return;
            }

            string fullMasterConfigPath = Path.Combine(_currentPackageLocation, _packageAssetsFolder, masterConfigFileName);
            _packageLog.Log($"PdxTaskManager: Attempting to load master task configuration from: {fullMasterConfigPath}", TraceEventType.Information);

            if (!File.Exists(fullMasterConfigPath))
            {
                _packageLog.Log($"PdxTaskManager: Master task configuration file '{fullMasterConfigPath}' not found. No PDX tasks will be executed.", TraceEventType.Warning);
                return; 
            }

            PdxMasterTaskConfig masterConfig;
            try
            {
                string masterConfigJson = File.ReadAllText(fullMasterConfigPath);
                var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                masterConfig = JsonSerializer.Deserialize<PdxMasterTaskConfig>(masterConfigJson, serializerOptions);
            }
            catch (Exception ex)
            {
                _packageLog.Log($"PdxTaskManager: Error reading or deserializing master task configuration file '{fullMasterConfigPath}': {ex.Message}", TraceEventType.Error, ex);
                throw;
            }

            if (masterConfig == null || masterConfig.Tasks == null || !masterConfig.Tasks.Any())
            {
                _packageLog.Log("PdxTaskManager: Master task configuration is empty or contains no tasks.", TraceEventType.Information);
                return;
            }

            _packageLog.Log($"PdxTaskManager: Found {masterConfig.Tasks.Count} task entries in master configuration.", TraceEventType.Information);
            int executedTaskCount = 0;

            foreach (var taskSetting in masterConfig.Tasks)
            {
                if (!taskSetting.Enabled)
                {
                    _packageLog.Log($"PdxTaskManager: Task '{taskSetting.TaskName}' is disabled in master config. Skipping.", TraceEventType.Information);
                    continue;
                }

                if (string.IsNullOrEmpty(taskSetting.TaskName) || string.IsNullOrEmpty(taskSetting.ConfigFile))
                {
                    _packageLog.Log($"PdxTaskManager: Invalid task entry in master config - TaskName or ConfigFile is missing. Entry: {JsonSerializer.Serialize(taskSetting)}", TraceEventType.Warning);
                    continue;
                }

                if (!_registeredTaskTypes.TryGetValue(taskSetting.TaskName, out Type taskType))
                {
                    _packageLog.Log($"PdxTaskManager: Task '{taskSetting.TaskName}' is specified in master config but no corresponding task type is registered. Skipping.", TraceEventType.Warning);
                    continue;
                }

                _packageLog.Log($"PdxTaskManager: Preparing to execute task '{taskSetting.TaskName}' with config '{taskSetting.ConfigFile}'.", TraceEventType.Information);
                IPdxTask taskInstance = null;
                try
                {
                    // Create instance of the task. Assumes constructor (IOrganizationService, TraceLogger).
                    // This requires the task classes to have this specific constructor.
                    taskInstance = (IPdxTask)Activator.CreateInstance(taskType, _service, _packageLog);
                }
                catch (Exception ex)
                {
                    _packageLog.Log($"PdxTaskManager: Failed to create an instance of task type '{taskType.FullName}' for task '{taskSetting.TaskName}': {ex.Message}", TraceEventType.Error, ex);
                    throw;
                }

                if (taskInstance == null)
                {
                     _packageLog.Log($"PdxTaskManager: Failed to create an instance of task type '{taskType.FullName}' (returned null) for task '{taskSetting.TaskName}'. Skipping.", TraceEventType.Error);
                     continue;
                }

                try
                {
                    // Execute the task. The task itself will handle its specific config file path resolution.
                    taskInstance.Execute(taskSetting.ConfigFile, _packageAssetsFolder, _currentPackageLocation);
                    _packageLog.Log($"PdxTaskManager: Task '{taskSetting.TaskName}' executed successfully.", TraceEventType.Information);
                    executedTaskCount++;
                }
                catch (Exception ex)
                {
                    _packageLog.Log($"PdxTaskManager: Task '{taskSetting.TaskName}' failed during execution: {ex.Message}", TraceEventType.Error, ex);
                    throw; 
                }
            }
            _packageLog.Log($"PdxTaskManager: Finished processing tasks. Executed {executedTaskCount} out of {masterConfig.Tasks.Count} configured task entries.", TraceEventType.Information);
        }
    }
} 