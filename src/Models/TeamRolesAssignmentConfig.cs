using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BeerLike.PDX.Models
{
    public class TeamRolesAssignmentConfig
    {
        [JsonPropertyName("teamId")]
        public Guid? TeamId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("securityRoleIds")]
        public List<Guid>? RoleIds { get; set; } = new List<Guid>();

        [JsonPropertyName("fieldSecurityProfileIds")]
        public List<Guid>? FieldSecurityProfileIds { get; set; } = new List<Guid>();
    }

    public class TeamAssignmentsConfig
    {
        [JsonPropertyName("teamAssignments")]
        public List<TeamRolesAssignmentConfig>? Assignments { get; set; }
    }
} 