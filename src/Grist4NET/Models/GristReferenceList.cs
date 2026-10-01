using System.Runtime.CompilerServices;

namespace Grist4NET;

internal interface IGristReferenceList
{
	public string TableId { get; }
	public IReadOnlyList<ulong> RowIds { get; }
	public int Count { get; }
}

public sealed class GristReferenceList : IGristReferenceList
{
	internal GristReferenceList() { }

	public string TableId { get; internal init; }
	public IReadOnlyList<ulong> RowIds { get; internal init; }
	public int Count => RowIds.Count;

	public static explicit operator ulong[](GristReferenceList r) => r.RowIds.ToArray();
}

public sealed class GristReferenceList<T> : IGristReferenceList
	where T : new()
{
	internal GristReferenceList() { }

	public string TableId { get; internal init; }
	public IReadOnlyList<ulong> RowIds { get; internal init; }
	public int Count => RowIds.Count;
	public IReadOnlyList<T> Values { get; internal init; }

	public static explicit operator ulong[](GristReferenceList<T> r) => r.RowIds.ToArray();
}
