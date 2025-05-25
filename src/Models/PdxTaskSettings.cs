using System.Collections.Generic;

namespace BeerLike.PDX.Models
{
    /// <summary>
    /// Represents a single task entry in the master task configuration file.
    /// </summary>
    public class PdxTaskSetting
    {
        /// <summary>
        /// The registered name of the task to execute (must match IPdxTask.TaskName).
        /// </summary>
        public string TaskName { get; set; }

        /// <summary>
        /// The task-specific configuration file (e.g., "TeamAssignments.json").
        /// This path is relative to the PkgAssets folder.
        /// </summary>
        public string ConfigFile { get; set; }

        /// <summary>
        /// Indicates whether this task should be executed.
        /// Defaults to true if not specified.
        /// </summary>
        public bool Enabled { get; set; } = true; // Default to true
    }

    /// <summary>
    /// Represents the root structure of the master task configuration file (e.g., pdx_tasks.json).
    /// </summary>
    public class PdxMasterTaskConfig
    {
        /// <summary>
        /// List of tasks to be executed.
        /// </summary>
        public List<PdxTaskSetting> Tasks { get; set; }

        public PdxMasterTaskConfig()
        {
            Tasks = new List<PdxTaskSetting>();
        }
    }
} 