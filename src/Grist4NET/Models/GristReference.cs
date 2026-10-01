namespace Grist4NET;

internal interface IGristReference
{
	public string TableId { get; }
	public ulong RowId { get; }
}

public sealed class GristReference : IGristReference
{
	internal GristReference() { }

	public string TableId { get; internal init; }
	public ulong RowId { get; internal init; }

	public static explicit operator ulong(GristReference r) => r.RowId;
}

public sealed class GristReference<T> : IGristReference
	where T : new()
{
	internal GristReference() { }

	public string TableId { get; internal init; }
	public ulong RowId { get; internal init; }
	public T Value { get; internal init; }

	public static explicit operator ulong(GristReference<T> r) => r.RowId;
}
