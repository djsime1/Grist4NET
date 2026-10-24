namespace Grist4NET;

internal interface IGristReference
{
	/// <summary>
	/// The table ID that this reference points to.
	/// </summary>
	public string TableId { get; }

	/// <summary>
	/// The specific row ID within the <see cref="TableId"/> that this reference points to.
	/// </summary>
	public ulong RowId { get; }
}

/// <summary>
/// A basic pointer returned for cells in a reference column.
/// </summary>
public sealed class GristReference : IGristReference
{
	internal GristReference() { }

	/// <inheritdoc />
	public string TableId { get; internal init; }

	/// <inheritdoc />
	public ulong RowId { get; internal init; }
}

#if DEBUG
public sealed class GristReference<T> : IGristReference
	where T : new()
{
	internal GristReference() { }

	public string TableId { get; internal init; }
	public ulong RowId { get; internal init; }
	public T Value { get; internal init; }
}
#endif
