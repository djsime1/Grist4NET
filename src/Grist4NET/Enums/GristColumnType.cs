namespace Grist4NET;

/// <summary>
/// What type of data a table column stores.
/// </summary>
public enum GristColumnType
{
	/// <summary>Any</summary>
	Any,

	/// <summary>Text</summary>
	Text,

	/// <summary>Numeric</summary>
	Numeric,

	/// <summary>Integer</summary>
	Int,

	/// <summary>Toggle</summary>
	Bool,

	/// <summary>Date</summary>
	Date,

	/// <summary>DateTime</summary>
	DateTime,

	/// <summary>Choice</summary>
	Choice,

	/// <summary>Choice List</summary>
	ChoiceList,

	/// <summary>Reference</summary>
	TableRef,

	/// <summary>Reference List</summary>
	TableRefList,

	/// <summary>Attachment</summary>
	Attachments,

	/// <summary>¯\\_(ツ)_/¯</summary>
	Unknown,
}

/// <summary>
/// Looser groupings for <see cref="GristColumnType"/>'s that roughly describe the type of data it represents.
/// </summary>
public enum GristColumnTypeCategory
{
	/// <summary>
	/// A <see cref="GristColumnType.Any"/>, <see cref="GristColumnType.Text"/>, <see cref="GristColumnType.Numeric"/>,
	/// <see cref="GristColumnType.Int"/>, or <see cref="GristColumnType.Bool"/>
	/// </summary>
	Value,

	/// <summary>
	/// A <see cref="GristColumnType.Date"/> or <see cref="GristColumnType.DateTime"/>
	/// </summary>
	Date,

	/// <summary>
	/// A <see cref="GristColumnType.Choice"/> or <see cref="GristColumnType.ChoiceList"/>
	/// </summary>
	Choice,

	/// <summary>
	/// A <see cref="GristColumnType.TableRef"/> or <see cref="GristColumnType.TableRefList"/>
	/// </summary>
	Reference,

	/// <summary>
	/// A <see cref="GristColumnType.Unknown"/>
	/// </summary>
	Unknown,
}

/// <summary>
/// Helper and extension methods for working with <see cref="GristColumnType"/> and
/// <see cref="GristColumnTypeCategory"/>.
/// </summary>
public static class GristColumnTypeHelpers
{
	/// <summary>
	/// Parses the enum-and-then-some string that the Grist API returns to describe a column type
	/// </summary>
	/// <param name="type">The "type" field from a Grist column API query</param>
	/// <param name="parameter">Set when a DateTime, Ref, or RefList column is given</param>
	public static GristColumnType GetWrapperEnum(string type, out string? parameter)
	{
		parameter = null;
		if (type.Contains(':'))
		{
			string[] split = type.Split(':');
			type = split[0];
			parameter = split[1];
		}

		return type switch
		{
			"Any" => GristColumnType.Any,
			"Text" => GristColumnType.Text,
			"Numeric" => GristColumnType.Numeric,
			"Int" => GristColumnType.Int,
			"Bool" => GristColumnType.Bool,
			"Date" => GristColumnType.Date,
			"DateTime" => GristColumnType.DateTime,
			"Choice" => GristColumnType.Choice,
			"ChoiceList" => GristColumnType.ChoiceList,
			"Ref" => GristColumnType.TableRef,
			"RefList" => GristColumnType.TableRefList,
			"Attachments" => GristColumnType.Attachments,
			null => throw new ArgumentNullException(type),
			_ => GristColumnType.Unknown,
		};
	}

	/// <summary>
	/// Get a <see cref="GristColumnType"/>'s corresponding <see cref="GristColumnTypeCategory"/>.
	/// See the docs of each category enum for the mappings.
	/// </summary>
	public static GristColumnTypeCategory GetCategory(this GristColumnType source)
	{
		return source switch
		{
			GristColumnType.Date or GristColumnType.DateTime => GristColumnTypeCategory.Date,
			GristColumnType.Choice or GristColumnType.ChoiceList => GristColumnTypeCategory.Choice,
			GristColumnType.TableRef or GristColumnType.TableRefList =>
				GristColumnTypeCategory.Reference,
			GristColumnType.Unknown => GristColumnTypeCategory.Unknown,
			_ => GristColumnTypeCategory.Value,
		};
	}
}
