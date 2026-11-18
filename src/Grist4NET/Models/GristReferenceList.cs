using System.Collections;

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
public sealed class GristReferenceList : IGristReferenceList, IReadOnlyList<ulong>
{
	internal GristReferenceList() { }

	/// <inheritdoc />
	public string TableId { get; internal init; }

	/// <inheritdoc />
	public IReadOnlyList<ulong> RowIds { get; internal init; }

	/// <inheritdoc />
	public int Count => RowIds.Count;

	public IEnumerator<ulong> GetEnumerator() => RowIds.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public ulong this[int index] => RowIds[index];
}

#if DEBUG
public sealed class GristReferenceList<T> : IGristReferenceList, IReadOnlyList<KeyValuePair<ulong, T>>
	where T : new()
{
	internal GristReferenceList() { }

	internal GristReferenceList(GristReferenceList source)
	{
		TableId = source.TableId;
		RowIds = source.RowIds;
	}

	public string TableId { get; internal init; }
	public IReadOnlyList<ulong> RowIds { get; internal init; }
	public int Count => RowIds.Count;
	public IReadOnlyList<T> Values { get; internal init; }

	public IEnumerator<KeyValuePair<ulong, T>> GetEnumerator() => Enumerable.Range(0, Count)
		.Select(i => new KeyValuePair<ulong, T>(RowIds[i], Values[i])).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public KeyValuePair<ulong, T> this[int i] => new(RowIds[i], Values[i]);
}
#endif
