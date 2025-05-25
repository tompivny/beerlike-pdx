using Microsoft.Xrm.Sdk;
using System.Text.Json;
using Microsoft.Xrm.Sdk.Client;
using System.Reflection;
using System.Diagnostics;
using Microsoft.Xrm.Tooling.PackageDeployment.CrmPackageExtentionBase;
using BeerLike.PDX.Services;
using BeerLike.PDX.Models;

[assembly: Microsoft.Xrm.Sdk.Client.ProxyTypesAssemblyAttribute()]

namespace BeerLike.PDX.Tasks
{
    public class DeclareTeamsRoles : IPdxTask
    {
        private readonly IOrganizationService _service;
        private readonly TraceLogger _packageLog;
        private readonly IConfigSchemaValidator _configValidator;
        private const string DefaultSchemaFileName = "TeamAssignments.schema.json";
        private const string TeamRoleRelationshipName = "teamroles_association";

        public string TaskName => "DeclareTeamsRolesTask";

        internal DeclareTeamsRoles(IOrganizationService service, TraceLogger packageLog)
            : this(service, packageLog, new ConfigSchemaValidator())
        {
        }

        internal DeclareTeamsRoles(IOrganizationService service, TraceLogger packageLog, IConfigSchemaValidator configValidator)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _packageLog = packageLog ?? throw new ArgumentNullException(nameof(packageLog));
            _configValidator = configValidator ?? throw new ArgumentNullException(nameof(configValidator));
        }

        public void Execute(string configFile, string packageAssetsFolder, string currentPackageLocation)
        {
            if (string.IsNullOrEmpty(configFile)) throw new ArgumentNullException(nameof(configFile), "Config file name for task cannot be null or empty.");
            if (string.IsNullOrEmpty(packageAssetsFolder)) throw new ArgumentNullException(nameof(packageAssetsFolder));
            if (string.IsNullOrEmpty(currentPackageLocation)) throw new ArgumentNullException(nameof(currentPackageLocation));

            _packageLog.Log($"{TaskName}: Starting. Task-specific Config File (relative to PkgAssets): {configFile}", TraceEventType.Information);

            string fullConfigPath = Path.Combine(currentPackageLocation, packageAssetsFolder, configFile);
            _packageLog.Log($"{TaskName}: Full path to task-specific config file: {fullConfigPath}", TraceEventType.Verbose);

            string configJson;
            string schemaJson;
            string schemaFilePath;
            try
            {
                if (!File.Exists(fullConfigPath))
                {
                    _packageLog.Log($"{TaskName}: Task-specific configuration file not found at {fullConfigPath}. Aborting task.", TraceEventType.Error);
                    throw new FileNotFoundException($"Task-specific configuration file not found: {fullConfigPath}", fullConfigPath);
                }
                configJson = File.ReadAllText(fullConfigPath);

                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                if (string.IsNullOrEmpty(assemblyDirectory))
                {
                    _packageLog.Log($"{TaskName}: Could not determine assembly directory for schema.", TraceEventType.Error);
                    throw new InvalidOperationException("Could not determine assembly directory for schema loading.");
                }
                schemaFilePath = Path.Combine(assemblyDirectory,"Schemas", DefaultSchemaFileName);

                _packageLog.Log($"{TaskName}: Attempting to read schema file from: {schemaFilePath}", TraceEventType.Verbose);
                if (!File.Exists(schemaFilePath))
                {
                    _packageLog.Log($"{TaskName}: Schema file not found at {schemaFilePath}. Aborting task.", TraceEventType.Error);
                    throw new FileNotFoundException($"Schema file not found: {schemaFilePath}", schemaFilePath);
                }
                schemaJson = File.ReadAllText(schemaFilePath);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                _packageLog.Log($"{TaskName}: Error accessing task-specific configuration or schema file: {ex.Message}", TraceEventType.Error, ex);
                throw;
            }
            catch (Exception ex)
            {
                _packageLog.Log($"{TaskName}: Unexpected error reading task-specific configuration/schema: {ex.Message}", TraceEventType.Error, ex);
                throw;
            }

            var isValid = _configValidator.Validate(configJson, schemaJson, _packageLog);
            if (!isValid)
            {
                var errorMessage = $"{TaskName}: Task-specific configuration file '{fullConfigPath}' failed schema validation against '{schemaFilePath}'. Check logs for details.";
                _packageLog.Log(errorMessage, TraceEventType.Error);
                throw new Exception(errorMessage);
            }
            _packageLog.Log($"{TaskName}: Task-specific configuration file validated successfully.", TraceEventType.Information);
            UpsertTeamsRoles(fullConfigPath, configJson);
        }

        private void UpsertTeamsRoles(string sourceConfigFile, string configJson)
        {
            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var config = JsonSerializer.Deserialize<TeamAssignmentsConfig>(configJson, serializerOptions);

            if (config?.Assignments == null || !config.Assignments.Any())
            {
                _packageLog.Log($"{TaskName}: No team role assignments found in '{sourceConfigFile}' or configuration is empty.", TraceEventType.Warning);
                return;
            }

            using (var context = new OrganizationServiceContext(_service))
            {
                foreach (var teamRolesAssignment in config.Assignments)
                {
                    ProcessTeamRoleAssignment(context, teamRolesAssignment);
                }

                try
                {
                    _packageLog.Log($"{TaskName}: Attempting to save team role changes from '{sourceConfigFile}' to Dataverse...", TraceEventType.Information);
                    context.SaveChanges();
                    _packageLog.Log($"{TaskName}: Team role changes from '{sourceConfigFile}' processed and saved successfully.", TraceEventType.Information);
                }
                catch (Exception ex)
                {
                    _packageLog.Log($"{TaskName}: Error saving team role changes from '{sourceConfigFile}': {ex.Message}", TraceEventType.Error, ex);
                    throw;
                }
            }
        }

        private void ProcessTeamRoleAssignment(OrganizationServiceContext context, TeamRolesAssignmentConfig assignmentConfig)
        {
            var teamEntity = assignmentConfig.TeamId.HasValue
                ? (from t in context.CreateQuery<Team>()
                   where t.TeamId == assignmentConfig.TeamId
                   select t).FirstOrDefault()
                : (from t in context.CreateQuery<Team>()
                   where t.Name == assignmentConfig.Name
                   select t).FirstOrDefault();

            if (teamEntity == null)
            {
                _packageLog.Log($"{TaskName}: Team with ID {assignmentConfig.TeamId} or name {assignmentConfig.Name} does not exist. Skipping assignment.", TraceEventType.Warning);
                return;
            }

            _packageLog.Log($"{TaskName}: Processing assignments for team: {teamEntity.Name} (ID: {teamEntity.TeamId}).", TraceEventType.Information);

            if (assignmentConfig.RoleIds == null)
            {
                _packageLog.Log($"No roles configured for team {teamEntity.Name} (ID: {teamEntity.TeamId}). Skipping processing.", TraceEventType.Information);
                return;
            }
            var validRoleIdsInConfig = GetValidRoleIdsFromConfig(context, teamEntity, assignmentConfig.RoleIds);
            var existingRoleIdsOnTeam = GetExistingTeamRoles(context, teamEntity.ToEntityReference());

            var rolesToAssociate = validRoleIdsInConfig.Except(existingRoleIdsOnTeam).ToList();
            var rolesToDisassociate = existingRoleIdsOnTeam.Except(validRoleIdsInConfig).ToList();

            AssociateRolesToTeam(context, teamEntity, rolesToAssociate);
            DisassociateRolesFromTeam(context, teamEntity, rolesToDisassociate);
        }

        private List<Guid> GetValidRoleIdsFromConfig(OrganizationServiceContext context, Team teamEntity, List<Guid> configuredRoleIds)
        {
            var validRoleIds = new List<Guid>();
            if (configuredRoleIds == null || !configuredRoleIds.Any())
            {
                _packageLog.Log($"{TaskName}: No roles configured for team {teamEntity.Name} (ID: {teamEntity.TeamId}).", TraceEventType.Verbose);
                return validRoleIds;
            }

            foreach (var roleId in configuredRoleIds)
            {
                if (RoleExists(context, roleId))
                {
                    validRoleIds.Add(roleId);
                }
                else
                {
                    _packageLog.Log($"{TaskName}: Role with ID {roleId} does not exist. It will be ignored for team {teamEntity.Name} (ID: {teamEntity.TeamId}).", TraceEventType.Warning);
                }
            }
            return validRoleIds;
        }

        private void AssociateRolesToTeam(OrganizationServiceContext context, Team teamEntity, List<Guid> rolesToAssociate)
        {
            if (rolesToAssociate == null || !rolesToAssociate.Any()) return;

            foreach (var roleIdToAssociate in rolesToAssociate)
            {
                var roleToAssociateEntity = (from r in context.CreateQuery<Role>()
                                             where r.RoleId == roleIdToAssociate
                                             select new Role { RoleId = r.RoleId, Name = r.Name }).FirstOrDefault();
                if (roleToAssociateEntity != null)
                {
                    try
                    {
                        context.AddLink(teamEntity, new Relationship(TeamRoleRelationshipName), roleToAssociateEntity);
                        _packageLog.Log($"{TaskName}: Prepared to associate role '{roleToAssociateEntity.Name}' (ID: {roleIdToAssociate}) with team '{teamEntity.Name}' (ID: {teamEntity.TeamId}).", TraceEventType.Information);
                    }
                    catch (Exception ex)
                    {
                        _packageLog.Log($"{TaskName}: Failed to prepare association of role '{roleToAssociateEntity.Name}' with team '{teamEntity.Name}': {ex.Message}", TraceEventType.Error, ex);
                    }
                }
                else
                {
                    _packageLog.Log($"{TaskName}: Role with ID {roleIdToAssociate} not found for association with team '{teamEntity.Name}'. It might have been deleted.", TraceEventType.Warning);
                }
            }
        }

        private void DisassociateRolesFromTeam(OrganizationServiceContext context, Team teamEntity, List<Guid> rolesToDisassociate)
        {
            if (rolesToDisassociate == null || !rolesToDisassociate.Any()) return;

            foreach (var roleIdToDisassociate in rolesToDisassociate)
            {
                var roleToDisassociateEntity = (from r in context.CreateQuery<Role>()
                                                where r.RoleId == roleIdToDisassociate
                                                select new Role { RoleId = r.RoleId, Name = r.Name }).FirstOrDefault();
                if (roleToDisassociateEntity != null)
                {
                    try
                    {
                        context.DeleteLink(teamEntity, new Relationship(TeamRoleRelationshipName), roleToDisassociateEntity);
                        _packageLog.Log($"{TaskName}: Prepared to disassociate role '{roleToDisassociateEntity.Name}' (ID: {roleIdToDisassociate}) from team '{teamEntity.Name}' (ID: {teamEntity.TeamId}).", TraceEventType.Information);
                    }
                    catch (Exception ex)
                    {
                        _packageLog.Log($"{TaskName}: Failed to prepare disassociation of role '{roleToDisassociateEntity.Name}' from team '{teamEntity.Name}': {ex.Message}", TraceEventType.Error, ex);
                    }
                }
                else
                {
                    _packageLog.Log($"{TaskName}: Role with ID {roleIdToDisassociate} not found for disassociation from team '{teamEntity.Name}'. It might have been already removed or deleted.", TraceEventType.Warning);
                }
            }
        }

        private bool RoleExists(OrganizationServiceContext context, Guid roleId)
        {
            return (from role in context.CreateQuery<Role>()
                    where role.RoleId == roleId
                    select role.RoleId)
                    .FirstOrDefault() != Guid.Empty;
        }

        private List<Guid> GetExistingTeamRoles(OrganizationServiceContext context, EntityReference teamReference)
        {
            var teamEntity = context.CreateQuery<Team>()
                                    .FirstOrDefault(t => t.TeamId == teamReference.Id);

            if (teamEntity == null)
            {
                _packageLog.Log($"{TaskName}: Team with ID {teamReference.Id} not found in GetExistingTeamRoles. Cannot retrieve existing roles for this team.", TraceEventType.Warning);
                return new List<Guid>();
            }

            _packageLog.Log($"{TaskName}: Retrieving existing roles for team '{teamEntity.Name}' (ID: {teamEntity.Id}).", TraceEventType.Verbose);

            try
            {
                if (!context.IsAttached(teamEntity))
                {
                    context.Attach(teamEntity);
                }
                context.LoadProperty(teamEntity, TeamRoleRelationshipName);
            }
            catch (Exception ex)
            {
                _packageLog.Log($"{TaskName}: Error loading '{TeamRoleRelationshipName}' property for team '{teamEntity.Name}' (ID: {teamEntity.Id}): {ex.Message}. Proceeding with potentially empty role set.", TraceEventType.Warning, ex);
            }

            if (teamEntity.teamroles_association != null)
            {
                var roleIds = teamEntity.teamroles_association
                                 .Where(role => role != null && role.Id != Guid.Empty)
                                 .Select(role => role.Id)
                                 .ToList();
                _packageLog.Log($"{TaskName}: Found {roleIds.Count} existing roles for team '{teamEntity.Name}'.", TraceEventType.Verbose);
                return roleIds;
            }

            _packageLog.Log($"{TaskName}: No existing roles found (or teamroles_association property is null) for team '{teamEntity.Name}'.", TraceEventType.Verbose);
            return new List<Guid>();
        }
    }
}
