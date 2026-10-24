using Grist4NET.Kiota.Models;

namespace Grist4NET;

// TODO: Add methods to create filters

/// <summary>
/// Base class for storing a column's properties and metadata.
/// May also be a <see cref="GristChoiceColumn"/> or <see cref="GristReferenceColumn"/>.
/// </summary>
public class GristColumn
{
	internal readonly string? Parameter;

	internal GristColumn(
		int columnRef,
		string columnId,
		string label,
		bool isFormula,
		GristColumnType type,
		string? description,
		string? parameter = null
	)
	{
		ColumnRef = columnRef;
		ColumnId = columnId;
		Label = label;
		IsFormula = isFormula;
		ColumnType = type;
		Description = description;
		Parameter = parameter;
	}

	internal GristColumn(ColumnsList_columns source)
		: this(
			(int)source.Fields!.ColRef!,
			source.Id!,
			source.Fields.Label!,
			(bool)source.Fields.IsFormula!,
			GristColumnTypeHelpers.GetWrapperEnum(source.Fields.Type!, out string? parameter),
			(string)source.Fields.AdditionalData["description"],
			parameter
		) { }

	/// <summary>
	/// Internal immutable column identifier used by Grist.
	/// </summary>
	public int ColumnRef { get; }

	/// <summary>
	/// Mutable column identifier.
	/// </summary>
	public string ColumnId { get; internal set; }

	/// <summary>
	/// Display name of the column.
	/// </summary>
	public string Label { get; internal set; }

	/// <summary>
	/// Whether cell values in this column are set by a formula.
	/// (In other words, this column is read-only if true)
	/// </summary>
	public bool IsFormula { get; internal set; }

	/// <summary>
	/// See <see cref="GristColumnType"/>.
	/// </summary>
	public GristColumnType ColumnType { get; internal set; }

	/// <summary>
	/// See <see cref="GristColumnTypeCategory"/>.
	/// </summary>
	public GristColumnTypeCategory ColumnTypeCategory => ColumnType.GetCategory();

	/// <summary>
	/// Extended description of the column.
	/// </summary>
	public string? Description { get; internal set; }
}

/// <summary>
/// Represents either a Choice or Choice List column, exposes <see cref="Choices"/>.
/// </summary>
public class GristChoiceColumn : GristColumn
{
	private readonly List<string> _choiceOptions;

	internal GristChoiceColumn(
		int columnRef,
		string columnId,
		string label,
		bool isFormula,
		GristColumnType type,
		string description,
		List<string> choices
	)
		: base(columnRef, columnId, label, isFormula, type, description)
	{
		_choiceOptions = choices;
	}

	internal GristChoiceColumn(ColumnsList_columns source, List<string> choices)
		: base(source)
	{
		_choiceOptions = choices;
	}

	/// <summary>
	/// Valid choices for data in this column, as defined within the Grist UI.
	/// </summary>
	public IReadOnlyList<string> Choices => _choiceOptions.AsReadOnly();
}

/// <summary>
/// Represents either a Reference or Reference List column, exposes <see cref="RefTableId"/>.
/// </summary>
public class GristReferenceColumn : GristColumn
{
	internal GristReferenceColumn(
		int columnRef,
		string columnId,
		string label,
		bool isFormula,
		GristColumnType type,
		string description,
		string refTableId
	)
		: base(columnRef, columnId, label, isFormula, type, description, refTableId) { }

	internal GristReferenceColumn(ColumnsList_columns source)
		: base(source) { }

	/// <summary>
	/// Table ID of which references in this column exist in.
	/// </summary>
	public string RefTableId => Parameter!;
}
