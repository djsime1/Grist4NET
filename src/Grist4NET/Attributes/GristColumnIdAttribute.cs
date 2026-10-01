namespace Grist4NET;

/// <summary>
/// When applied to a property, it will be mapped to this Column ID specifically, instead of mapping by property name.
/// </summary>
/// <param name="columnId">Column ID to map</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class GristColumnIdAttribute(string columnId) : Attribute
{
	/// <summary>
	/// Column ID to map
	/// </summary>
	public string ColumnId { get; } = columnId;
}
