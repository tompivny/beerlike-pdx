# BeerLike.PDX

## Overview

BeerLike.PDX is a .NET library that extends the capabilities of [Package Deployer](https://learn.microsoft.com/en-us/power-platform/alm/package-deployer-tool) for Microsoft Dynamics 365 CE / Power Platform to enhance and automate deployment process of Power Platform solutions.

## Key Features

The library currently provides the following tasks:

1. **Team ↔ Security Roles Associations**: Declaratively sets security roles for Dataverse teams
2. **Team ↔ Column Security Profiles Associations**: Declaratively sets column security profiles for Dataverse teams

All configurations are defined through JSON files with schema validation, ensuring consistency and reducing deployment errors.

## How to Consume in Package Deployer

### 1. Add Library Reference

Navigate to your Package Deployer project and add the BeerLike.PDX library:

```bash
dotnet add package BeerLike.PDX
```

### 2. Add the main Configuration file

We need a config file that manages which PDX tasks are executed.

Create a JSON file in the root of your `PkgAssets` folder:

```powershell
New-Item -ItemType File PkgAssets\PDX.Tasks.json
```

Add the following content to the JSON file

```json
{
  "tasks":[]
}
```



```json
{
  "tasks": [
    {
      "taskName": "DeclareTeamsRolesTask",
      "configFile": "PDX.TeamAssignments.json",
      "enabled": true
    },
    {
      "taskName": "DeclareTeamsColumnSecurityProfiles",
      "configFile": "PDX.TeamAssignments.json",
      "enabled": true
    }
  ]
}
```



### 3. Configure PackageImportExtension

In the `AfterPrimaryImport()` method in your `PackageImportExtension.cs`, initialize the `PdxTaskManager` and execute tasks:

```csharp
public override bool AfterPrimaryImport()
{
    new BeerLike.PDX.PdxTaskManager(
        CrmSvc,
        PackageLog,
        GetImportPackageDataFolderName,
        CurrentPackageLocation).ExecuteTasks("PDX.Tasks.json");
    return true;
}
```

### 4. Configure specific PDX tasks
_will be implemented as a wiki_ 


#### PDX.TeamAssignments.json

This file contains the actual team assignment configurations:

```json
{
  "teamAssignments": [
    {
      "teamId": "05c06b4a-022c-f011-8c4e-7c1e5274ec5f",
      "securityRoleIds": ["74dd0a7f-ff2b-f011-8c4e-7c1e5274ec5f"],
      "fieldSecurityProfileIds": ["b1856d5c-002c-f011-8c4e-7c1e5274ec5f"]
    },
    {
      "name": "Sales Team",
      "securityRoleIds": [
        "8d53d5a1-022c-f011-8c4e-7c1e5274ec5f",
        "9a64e6b2-133d-f122-9d5f-8e2f6385fd6g"
      ],
      "fieldSecurityProfileIds": ["c2967f6d-113d-f122-9d5f-8e2f6385fd6g"]
    }
  ]
}
```

**Team Assignment Properties:**

- `teamId` (optional): Unique identifier (GUID) of the team
- `name` (optional): Name of the team (used if teamId is not provided)
- `securityRoleIds` (optional): Array of security role GUIDs to assign to the team
- `fieldSecurityProfileIds` (optional): Array of field security profile GUIDs to assign to the team

**Note:** Either `teamId` or `name` must be provided for each team assignment. Using `teamId` is recommended as long as you have unified IDs across your environments.

## Task Descriptions

### DeclareTeamsRolesTask

This task manages the association between Dataverse teams and security roles. It will:

- **Add missing associations**: Assign security roles to teams that don't currently have them
- **Remove extra associations**: Remove security roles from teams that shouldn't have them according to the configuration
- **Maintain existing associations**: Keep roles that are correctly assigned

The task performs a complete synchronization, ensuring the team's security roles match exactly what's defined in the configuration.

### DeclareTeamsColumnSecurityProfiles

This task manages the association between Dataverse teams and field security profiles. It will:

- **Add missing associations**: Assign field security profiles to teams that don't currently have them
- **Remove extra associations**: Remove field security profiles from teams that shouldn't have them according to the configuration
- **Maintain existing associations**: Keep profiles that are correctly assigned

Like the roles task, this performs complete synchronization of field security profile assignments.

## Schema Validation

All JSON configuration files are validated against JSON schemas to ensure:

- Correct structure and data types
- Valid GUID formats for IDs
- Required properties are present
- No additional unexpected properties

Validation occurs during package deployment, and deployment will fail if configuration files don't match the expected schema.

## Project Structure

```
beerlike-pdx/
├── src/
│   ├── Tasks/                        # Task implementations
│   ├── Services/                     # Supporting services
│   ├── Models/                       # Configuration models
│   ├── Schemas/                      # JSON schemas
│   ├── PdxTaskManager.cs             # Main task orchestrator
│   └── BeerLike.PDX.csproj           # Project file
├── BeerLike.PDX.sln                  # Solution file
└── README.md                         # This file
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

For issues and feature requests, please create an issue in the repository's issue tracker.
