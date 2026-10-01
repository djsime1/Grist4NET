using System.Diagnostics.CodeAnalysis;

namespace Grist4NET;

/*
From https://github.com/gristlabs/grist-core/blob/main/documentation/grist-data-format.md#typed-cell-values
L 	List 	["L", item, ...] 	A list of values. Used for ChoiceList, RefList, and Attachments
l 	LookUp 	["l", value, options] 	An instruction to set a Ref/RefList by looking up the given value rather than specifying a row ID directly. Used when sending values via the API, not in responses
O 	Dict 	["O", {key: value}] 	A dictionary/object
D 	DateTime 	["D", timestamp, timezone] 	DateTime value, e.g. ["D", 1704945919, "UTC"]
d 	Date 	["d", timestamp] 	Date value, e.g. ["d", 1704844800]
R 	Reference 	["R", tableId, rowId] 	Reference, e.g. ["R", "People", 17]
r 	ReferenceList 	["r", tableId, [rowId, ...]] 	Reference list, e.g. ["r", "People", [1, 2]]
E 	Exception 	["E", name, message?, details?] 	A formula error
P 	Pending 	["P"] 	Value is not yet computed
C 	Censored 	["C"] 	Value hidden by access rules
U 	Unmarshallable 	["U", repr] 	Value that could not be serialized
V 	Versions 	["V", versionObj] 	Used in document comparisons
S 	Skip 	["S"] 	Placeholder used in diffs to indicate unchanged rows
*/

/// <summary>
/// When a cell value is an array, its first element is a single-character type code. These are called "typed cell
/// values" and represent lists, errors, and other structured data.
/// </summary>
public enum GristCellType
{
	/// <summary>A list of values. Used for ChoiceList, RefList, and Attachments</summary>
	List,

	/// <summary>An instruction to set a Ref/RefList by looking up the given value rather than specifying a row ID directly. Used when sending values via the API, not in responses</summary>
	LookUp,

	/// <summary>A dictionary/object</summary>
	Dict,

	/// <summary>DateTime value, e.g. ["D", 1704945919, "UTC"]</summary>
	DateTime,

	/// <summary>Date value, e.g. ["d", 1704844800]</summary>
	Date,

	/// <summary>Reference, e.g. ["R", "People", 17]</summary>
	Reference,

	/// <summary>Reference list, e.g. ["r", "People", [1, 2]]</summary>
	ReferenceList,

	/// <summary>A formula error</summary>
	Exception,

	/// <summary>Value is not yet computed</summary>
	Pending,

	/// <summary>Value hidden by access rules</summary>
	Censored,

	/// <summary>Value that could not be serialized</summary>
	Unmarshallable,

	/// <summary>Used in document comparisons</summary>
	Versions,

	/// <summary>Placeholder used in diffs to indicate unchanged rows</summary>
	Skip,

	/// <summary>Fallback when type code is unrecognized</summary>
	Unknown,
}

/// <summary>
/// Methods to convert cell type codes into <see cref="GristCellType"/> enums.
/// </summary>
public static class GristCellTypeHelpers
{
	/// <summary>
	/// Maps type codes to <see cref="GristCellType"/> members
	/// </summary>
	/// <param name="code">Single-letter type code</param>
	/// <seealso href="https://github.com/gristlabs/grist-core/blob/main/documentation/grist-data-format.md#typed-cell-values">Grist API Documentation</seealso>
	public static GristCellType GetWrapperEnum(string code)
	{
		return code switch
		{
			"L" => GristCellType.List,
			"l" => GristCellType.LookUp,
			"O" => GristCellType.Dict,
			"D" => GristCellType.DateTime,
			"d" => GristCellType.Date,
			"R" => GristCellType.Reference,
			"r" => GristCellType.ReferenceList,
			"E" => GristCellType.Exception,
			"P" => GristCellType.Pending,
			"C" => GristCellType.Censored,
			"U" => GristCellType.Unmarshallable,
			"V" => GristCellType.Versions,
			"S" => GristCellType.Skip,
			null => throw new ArgumentNullException(code),
			_ => GristCellType.Unknown,
		};
	}

	/// <summary>
	/// Behaves the same as <see cref="GetWrapperEnum"/> when length 1 string is provided
	/// </summary>
	/// <param name="code">Type code</param>
	/// <param name="type">Set when a <see cref="GristCellType"/> can be mapped (Including <see cref="GristCellType.Unknown"/>)</param>
	/// <returns>True if length 1 string is provided, false otherwise</returns>
	/// <seealso href="https://github.com/gristlabs/grist-core/blob/main/documentation/grist-data-format.md#typed-cell-values">Grist API Documentation</seealso>
	public static bool TryGetWrapperEnum(
		string? code,
		[MaybeNullWhen(false)] out GristCellType? type
	)
	{
		type = null;
		if (code is not { Length: 1 })
			return false;
		type = GetWrapperEnum(code);
		return true;
	}
}
