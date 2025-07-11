#pragma warning disable CS1591
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

[assembly: Microsoft.Xrm.Sdk.Client.ProxyTypesAssemblyAttribute()]

namespace BeerLike.PDX.Models
{
	
	
	/// <summary>
	/// Represents a source of entities bound to a Dataverse service. It tracks and manages changes made to the retrieved entities.
	/// </summary>
	[System.CodeDom.Compiler.GeneratedCodeAttribute("Dataverse Model Builder", "2.0.0.11")]
	public partial class OrgContext : Microsoft.Xrm.Sdk.Client.OrganizationServiceContext
	{
		
		/// <summary>
		/// Constructor.
		/// </summary>
		public OrgContext(Microsoft.Xrm.Sdk.IOrganizationService service) : 
				base(service)
		{
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="BeerLike.PDX.Models.FieldSecurityProfile"/> entities.
		/// </summary>
		public System.Linq.IQueryable<BeerLike.PDX.Models.FieldSecurityProfile> FieldSecurityProfileSet
		{
			get
			{
				return this.CreateQuery<BeerLike.PDX.Models.FieldSecurityProfile>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="BeerLike.PDX.Models.Role"/> entities.
		/// </summary>
		public System.Linq.IQueryable<BeerLike.PDX.Models.Role> RoleSet
		{
			get
			{
				return this.CreateQuery<BeerLike.PDX.Models.Role>();
			}
		}
		
		/// <summary>
		/// Gets a binding to the set of all <see cref="BeerLike.PDX.Models.Team"/> entities.
		/// </summary>
		public System.Linq.IQueryable<BeerLike.PDX.Models.Team> TeamSet
		{
			get
			{
				return this.CreateQuery<BeerLike.PDX.Models.Team>();
			}
		}
	}
}
#pragma warning restore CS1591
