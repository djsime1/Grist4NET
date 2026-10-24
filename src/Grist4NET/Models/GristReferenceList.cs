namespace Grist4NET;

internal interface IGristReferenceList
{
	/// <summary>
	/// The table ID that these references point to.
	/// </summary>
	public string TableId { get; }

	/// <summary>
	/// The specific row ID's within the <see cref="TableId"/> that these references point to.
	/// </summary>
	public IReadOnlyList<ulong> RowIds { get; }

	/// <summary>
	/// The number of references present.
	/// </summary>
	public int Count { get; }
}

/// <summary>
/// A series of pointers returned for cells in a reference list column.
/// </summary>
public sealed class GristReferenceList : IGristReferenceList
{
	internal GristReferenceList() { }

	/// <inheritdoc />
	public string TableId { get; internal init; }

	/// <inheritdoc />
	public IReadOnlyList<ulong> RowIds { get; internal init; }

	/// <inheritdoc />
	public int Count => RowIds.Count;
}

#if DEBUG
public sealed class GristReferenceList<T> : IGristReferenceList
	where T : new()
{
	internal GristReferenceList() { }

	public string TableId { get; internal init; }
	public IReadOnlyList<ulong> RowIds { get; internal init; }
	public int Count => RowIds.Count;
	public IReadOnlyList<T> Values { get; internal init; }
}
#endif
