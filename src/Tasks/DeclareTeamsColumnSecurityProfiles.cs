using Microsoft.Xrm.Sdk;
using System.Text.Json;
using System.Reflection;
using System.Diagnostics;
using Microsoft.Xrm.Tooling.PackageDeployment.CrmPackageExtentionBase;
using BeerLike.PDX.Services;
using BeerLike.PDX.Models;

namespace BeerLike.PDX.Tasks
{
    public class DeclareTeamsColumnSecurityProfiles : IPdxTask
    {
        private readonly IOrganizationService _service;
        private readonly TraceLogger _packageLog;
        private readonly IConfigSchemaValidator _configValidator;
        private const string DefaultSchemaFileName = "TeamAssignments.schema.json";

        public string TaskName => "DeclareTeamsColumnSecurityProfiles";

        public DeclareTeamsColumnSecurityProfiles(IOrganizationService service, TraceLogger packageLog)
            : this(service, packageLog, new ConfigSchemaValidator())
        {
        }

        internal DeclareTeamsColumnSecurityProfiles(IOrganizationService service, TraceLogger packageLog, IConfigSchemaValidator configValidator)
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
            UpsertTeamsFieldSecurityProfiles(fullConfigPath, configJson);
        }

        private void UpsertTeamsFieldSecurityProfiles(string sourceConfigFile, string configJson)
        {
            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var config = JsonSerializer.Deserialize<TeamAssignmentsConfig>(configJson, serializerOptions);

            if (config?.Assignments == null || !config.Assignments.Any())
            {
                _packageLog.Log($"{TaskName}: No team field security profile assignments found in '{sourceConfigFile}' or configuration is empty.", TraceEventType.Warning);
                return;
            }

            using (var context = new OrgContext(_service))
            {
                foreach (var teamAssignment in config.Assignments)
                {
                    ProcessTeamFieldSecurityProfileAssignment(context, teamAssignment);
                }

                try
                {
                    _packageLog.Log($"{TaskName}: Attempting to save team field security profile changes from '{sourceConfigFile}' to Dataverse...", TraceEventType.Information);
                    context.SaveChanges();
                    _packageLog.Log($"{TaskName}: Team field security profile changes from '{sourceConfigFile}' processed and saved successfully.", TraceEventType.Information);
                }
                catch (Exception ex)
                {
                    _packageLog.Log($"{TaskName}: Error saving team field security profile changes from '{sourceConfigFile}': {ex.Message}", TraceEventType.Error, ex);
                    throw;
                }
            }
        }

        private void ProcessTeamFieldSecurityProfileAssignment(OrgContext context, TeamRolesAssignmentConfig assignmentConfig)
        {
            var teamEntity = assignmentConfig.TeamId.HasValue
                ? context.TeamSet.FirstOrDefault(t => t.TeamId == assignmentConfig.TeamId)
                : context.TeamSet.FirstOrDefault(t => t.Name == assignmentConfig.Name);

            if (teamEntity == null)
            {
                _packageLog.Log($"{TaskName}: Team with ID {assignmentConfig.TeamId} or name {assignmentConfig.Name} does not exist. Skipping assignment.", TraceEventType.Warning);
                return;
            }

            _packageLog.Log($"{TaskName}: Processing field security profile assignments for team: {teamEntity.Name} (ID: {teamEntity.TeamId}).", TraceEventType.Information);

            if (assignmentConfig.FieldSecurityProfileIds == null)
            {
                _packageLog.Log($"No field security profiles configured for team {teamEntity.Name} (ID: {teamEntity.TeamId}). Skipping processing.", TraceEventType.Information);
                return;
            }

            var validProfileIdsInConfig = GetValidFieldSecurityProfileIdsFromConfig(context, teamEntity, assignmentConfig.FieldSecurityProfileIds);
            var existingProfileIdsOnTeam = GetExistingTeamFieldSecurityProfiles(context, teamEntity.ToEntityReference());

            var profilesToAssociate = validProfileIdsInConfig.Except(existingProfileIdsOnTeam).ToList();
            var profilesToDisassociate = existingProfileIdsOnTeam.Except(validProfileIdsInConfig).ToList();

            AssociateFieldSecurityProfilesToTeam(context, teamEntity, profilesToAssociate);
            DisassociateFieldSecurityProfilesFromTeam(context, teamEntity, profilesToDisassociate);
        }

        private List<Guid> GetValidFieldSecurityProfileIdsFromConfig(OrgContext context, Team teamEntity, List<Guid> configuredProfileIds)
        {
            var validProfileIds = new List<Guid>();
            if (configuredProfileIds == null || !configuredProfileIds.Any())
            {
                _packageLog.Log($"{TaskName}: No field security profiles configured for team {teamEntity.Name} (ID: {teamEntity.TeamId}).", TraceEventType.Verbose);
                return validProfileIds;
            }

            foreach (var profileId in configuredProfileIds)
            {
                if (FieldSecurityProfileExists(context, profileId))
                {
                    validProfileIds.Add(profileId);
                }
                else
                {
                    _packageLog.Log($"{TaskName}: Field Security Profile with ID {profileId} does not exist. It will be ignored for team {teamEntity.Name} (ID: {teamEntity.TeamId}).", TraceEventType.Warning);
                }
            }
            return validProfileIds;
        }

        private void AssociateFieldSecurityProfilesToTeam(OrgContext context, Team teamEntity, List<Guid> profilesToAssociate)
        {
            if (profilesToAssociate == null || !profilesToAssociate.Any()) return;

            foreach (var profileIdToAssociate in profilesToAssociate)
            {
                var profileToAssociateEntity = context.FieldSecurityProfileSet
                                                     .Where(p => p.FieldSecurityProfileId == profileIdToAssociate)
                                                     .Select(p => new FieldSecurityProfile { FieldSecurityProfileId = p.FieldSecurityProfileId, Name = p.Name })
                                                     .FirstOrDefault();
                if (profileToAssociateEntity != null)
                {
                    try
                    {
                        context.AddLink(teamEntity, new Relationship(FieldSecurityProfile.Fields.teamprofiles_association), profileToAssociateEntity);
                        _packageLog.Log($"{TaskName}: Prepared to associate field security profile '{profileToAssociateEntity.Name}' (ID: {profileIdToAssociate}) with team '{teamEntity.Name}' (ID: {teamEntity.TeamId}).", TraceEventType.Information);
                    }
                    catch (Exception ex)
                    {
                        _packageLog.Log($"{TaskName}: Failed to prepare association of field security profile '{profileToAssociateEntity.Name}' with team '{teamEntity.Name}': {ex.Message}", TraceEventType.Error, ex);
                    }
                }
                else
                {
                    _packageLog.Log($"{TaskName}: Field Security Profile with ID {profileIdToAssociate} not found for association with team '{teamEntity.Name}'. It might have been deleted.", TraceEventType.Warning);
                }
            }
        }

        private void DisassociateFieldSecurityProfilesFromTeam(OrgContext context, Team teamEntity, List<Guid> profilesToDisassociate)
        {
            if (profilesToDisassociate == null || !profilesToDisassociate.Any()) return;

            foreach (var profileIdToDisassociate in profilesToDisassociate)
            {
                var profileToDisassociateEntity = context.FieldSecurityProfileSet
                                                         .Where(p => p.FieldSecurityProfileId == profileIdToDisassociate)
                                                         .Select(p => new FieldSecurityProfile { FieldSecurityProfileId = p.FieldSecurityProfileId, Name = p.Name })
                                                         .FirstOrDefault();
                if (profileToDisassociateEntity != null)
                {
                    try
                    {
                        context.DeleteLink(teamEntity, new Relationship(FieldSecurityProfile.Fields.teamprofiles_association), profileToDisassociateEntity);
                        _packageLog.Log($"{TaskName}: Prepared to disassociate field security profile '{profileToDisassociateEntity.Name}' (ID: {profileIdToDisassociate}) from team '{teamEntity.Name}' (ID: {teamEntity.TeamId}).", TraceEventType.Information);
                    }
                    catch (Exception ex)
                    {
                        _packageLog.Log($"{TaskName}: Failed to prepare disassociation of field security profile '{profileToDisassociateEntity.Name}' from team '{teamEntity.Name}': {ex.Message}", TraceEventType.Error, ex);
                    }
                }
                else
                {
                    _packageLog.Log($"{TaskName}: Field Security Profile with ID {profileIdToDisassociate} not found for disassociation from team '{teamEntity.Name}'. It might have been already removed or deleted.", TraceEventType.Warning);
                }
            }
        }

        private bool FieldSecurityProfileExists(OrgContext context, Guid profileId)
        {
            return context.FieldSecurityProfileSet
                         .Where(p => p.FieldSecurityProfileId == profileId)
                         .Select(p => p.FieldSecurityProfileId)
                         .FirstOrDefault() != Guid.Empty;
        }

        private List<Guid> GetExistingTeamFieldSecurityProfiles(OrgContext context, EntityReference teamReference)
        {
            var teamEntity = context.TeamSet.FirstOrDefault(t => t.TeamId == teamReference.Id);

            if (teamEntity == null)
            {
                _packageLog.Log($"{TaskName}: Team with ID {teamReference.Id} not found in GetExistingTeamFieldSecurityProfiles. Cannot retrieve existing field security profiles for this team.", TraceEventType.Warning);
                return new List<Guid>();
            }

            _packageLog.Log($"{TaskName}: Retrieving existing field security profiles for team '{teamEntity.Name}' (ID: {teamEntity.Id}).", TraceEventType.Verbose);

            try
            {
                if (!context.IsAttached(teamEntity))
                {
                    context.Attach(teamEntity);
                }
                context.LoadProperty(teamEntity, FieldSecurityProfile.Fields.teamprofiles_association);
            }
            catch (Exception ex)
            {
                _packageLog.Log($"{TaskName}: Error loading '{FieldSecurityProfile.Fields.teamprofiles_association}' property for team '{teamEntity.Name}' (ID: {teamEntity.Id}): {ex.Message}. Proceeding with potentially empty profile set.", TraceEventType.Warning, ex);
            }

            if (teamEntity.teamprofiles_association != null)
            {
                var profileIds = teamEntity.teamprofiles_association
                                          .Where(profile => profile != null && profile.Id != Guid.Empty)
                                          .Select(profile => profile.Id)
                                          .ToList();
                _packageLog.Log($"{TaskName}: Found {profileIds.Count} existing field security profiles for team '{teamEntity.Name}'.", TraceEventType.Verbose);
                return profileIds;
            }

            _packageLog.Log($"{TaskName}: No existing field security profiles found (or teamprofiles_association property is null) for team '{teamEntity.Name}'.", TraceEventType.Verbose);
            return new List<Guid>();
        }
    }
}

