using System.Collections;

namespace Grist4NET;

public interface IGristRecordFilter : IEnumerable
{
	public string ColumnId { get; }
	public IList GetValues();
}

public struct GristRecordFilter<T> : IGristRecordFilter, IEnumerable<T>
{
	public GristRecordFilter(string columnId, IEnumerable<T>? values = null)
	{
		ColumnId = columnId;
		if (values != null)
			Values.AddRange(values);
	}

	public GristRecordFilter(GristColumn column, IEnumerable<T>? values = null)
		: this(column.ColId, values) { }

	public string ColumnId { get; }
	public List<T> Values { get; } = new();

	public IList GetValues() => Values;

	public void Add(T value) => Values.Add(value);

	public IEnumerator<T> GetEnumerator() => Values.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
