using Grist4NET.Kiota.Models;

namespace Grist4NET;

/// <summary>
/// Base class for storing a column's properties and metadata.
/// May also be a <see cref="GristChoiceColumn"/> or <see cref="GristReferenceColumn"/>.
/// </summary>
public class GristColumn
{
	internal readonly string? Parameter;

	internal GristColumn(
		int colRef,
		string colId,
		string label,
		bool isFormula,
		GristColumnType type,
		string description,
		string? parameter = null
	)
	{
		ColRef = colRef;
		ColId = colId;
		Label = label;
		IsFormula = isFormula;
		Type = type;
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

	public int ColRef { get; }
	public string ColId { get; internal set; }
	public string Label { get; internal set; }
	public bool IsFormula { get; internal set; }
	public GristColumnType Type { get; internal set; }
	public GristColumnTypeCategory TypeCategory => Type.GetCategory();
	public string Description { get; internal set; }
}

/// <summary>
/// Represents either a Choice or Choice List column, exposes <see cref="Choices"/>.
/// </summary>
public class GristChoiceColumn : GristColumn
{
	private readonly List<string> _choiceOptions;

	internal GristChoiceColumn(
		int colRef,
		string colId,
		string label,
		bool isFormula,
		GristColumnType type,
		string description,
		List<string> choices
	)
		: base(colRef, colId, label, isFormula, type, description)
	{
		_choiceOptions = choices;
	}

	internal GristChoiceColumn(ColumnsList_columns source, List<string> choices)
		: base(source)
	{
		_choiceOptions = choices;
	}

	/// <summary>
	/// Valid choices for data in this column, as defined within Grist
	/// </summary>
	public IReadOnlyList<string> Choices => _choiceOptions.AsReadOnly();
}

/// <summary>
/// Represents either a Reference or Reference List column, exposes <see cref="RefTableId"/>.
/// </summary>
public class GristReferenceColumn : GristColumn
{
	internal GristReferenceColumn(
		int colRef,
		string colId,
		string label,
		bool isFormula,
		GristColumnType type,
		string description,
		string refTableId
	)
		: base(colRef, colId, label, isFormula, type, description, refTableId) { }

	internal GristReferenceColumn(ColumnsList_columns source)
		: base(source) { }

	/// <summary>
	/// Table ID of which column references in this column exist in
	/// </summary>
	public string RefTableId => Parameter!;
}
