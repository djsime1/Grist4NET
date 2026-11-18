using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Kiota.Abstractions.Serialization;

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

namespace Grist4NET;

public class GristRecordSet : IParsable, IReadOnlyCollection<GristRecordSet.GristRecord>, INotifyCollectionChanged
{
	[Flags]
	public enum GristRecordState
	{
		New = 1 << 0,
		Existing = 1 << 1,
		Modified = 1 << 2,
		MarkedForDeletion = 1 << 3,
		All = New | Existing | Modified | MarkedForDeletion
	}

	public class GristRecord : IReadOnlyDictionary<string, object?>, IReadOnlyList<object?>
	{
		private readonly GristRecordSet _source;
		private readonly int _index;
		private readonly int _offset;
		private ArraySegment<object?> _data;
		#if NETSTANDARD2_0
		private IList<object?> _dataList => _data;
		#endif
		public GristRecordSet Source => _source;
		public int Count => _source._colCount;
		public GristTable Table => _source.Table;
		public string TableId => _source.TableId;
		public GristDocument Document => _source.Document;
		public string DocumentId => _source.DocumentId;
		public ulong? RowId { get; }
		public bool IsModified { get; private set; }
		public bool IsNew => !RowId.HasValue;
		public bool IsMarkedForDeletion => RowId.HasValue && _source._deletedRecords.Contains(RowId.Value);

		internal GristRecord(GristRecordSet source, ulong rowid)
		{
			if (!source._baseRowIndex.TryGetValue(rowid, out _index))
				throw new ArgumentOutOfRangeException(nameof(rowid));
			_source = source;
			RowId = rowid;
			IsModified = _source._modifiedRecords.ContainsKey(RowId.Value);
			_offset = _index * Count;
			_data = IsModified
				? new ArraySegment<object?>(source._modifiedRecords[rowid])
				: new ArraySegment<object?>(source._baseRecords, _offset, Count);
			Debug.Assert(_data.Count == Count, "Record span lenght doesn't match column count");
		}

		internal GristRecord(GristRecordSet source, int newidx)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(newidx, source._newRecords.Count);
			_source = source;
			IsModified = false;
			_index = newidx;
			_data = new ArraySegment<object?>(source._newRecords[newidx]);
			Debug.Assert(_data.Count == Count, "Record span lenght doesn't match column count");
		}

		IEnumerator<object?> IEnumerable<object?>.GetEnumerator() => _data.GetEnumerator();

		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => Enumerable.Range(0, Count)
				.Select(i => new KeyValuePair<string, object?>(_source._colNameByIdx[i], GetValue(i))).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		
		public bool ContainsKey(string key) => _source._colSet.Contains(key);

		public bool TryGetValue(string key, [UnscopedRef] out object? value)
		{
			if (_source._colSet.Contains(key))
			{
				value = this[key];
				return true;
			}

			value = null;
			return false;
		}

		public bool TryGetValue<T>(string key, [UnscopedRef] out T? value)
		{
			if (_source._colSet.Contains(key))
			{
				value = GetValue<T>(key);
				return true;
			}

			value = default;
			return false;
		}

		public object? this[string key]
		{
			get => GetValue(_source._colIdxByName[key]);
			set
			{
				if (_source.Table.Columns[key].IsReadOnly)
					throw new NotSupportedException($"{TableId}.{key} is a read-only column.");
				SetValue(_source._colIdxByName[key], value);
			}
		}
		
		public object? this[int index]
		{
			get => GetValue(index);
			set
			{
				string key = _source._colNameByIdx[index];
				if (_source.Table.Columns[key].IsReadOnly)
					throw new NotSupportedException($"{TableId}.{key} is a read-only column.");
				SetValue(index, value);
			}
		}

		public T? GetValue<T>(string key)
		{
			object? value = this[key];
			if (value is T typedValue)
				return typedValue;
			return value != null ? GristValueConverter.ConvertValue<T>(value) : default;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private object? GetValue(int colIdx) =>
#if NETSTANDARD2_0
			_dataList[colIdx];
#else
			_data[colIdx];
#endif

		private void SetValue(int colIdx, object? value)
		{
			if (colIdx >= Count)
				throw new IndexOutOfRangeException();
			if (!IsNew && !IsModified)
			{
				object?[] data = new object?[Count];
				_data.CopyTo(data);
				_source._modifiedRecords[RowId!.Value] = data;
				_data = new ArraySegment<object?>(data);
				IsModified = true;
			}

#if NETSTANDARD2_0
			_dataList[colIdx] = value;
#else
			_data[colIdx] = value;
#endif
		}

		public IEnumerable<string> Keys => _source._colNameByIdx;

		public IEnumerable<object?> Values => _data;
	}

	private class GristRecordSetEnumerator : IEnumerator<GristRecord>
	{
		private readonly GristRecordSet _source;
		private readonly int _total;
		private readonly bool _searchNewRecords;
		private readonly ulong[] _baseRecords;
		private int _pos = -1;

		internal GristRecordSetEnumerator(GristRecordSet source, GristRecordState filter = GristRecordState.All)
		{
			_source = source;
			_total = source.CountOf(filter);
			_searchNewRecords = filter.HasFlag(GristRecordState.New);
			source.CollectionChanged += SourceOnCollectionChanged;
			
			IEnumerable<ulong> baseCandidates;
			if (filter.HasFlag(GristRecordState.Existing))
			{
				baseCandidates = source._baseRowIndex.Keys;
				if (!filter.HasFlag(GristRecordState.Modified))
					baseCandidates = baseCandidates.Except(source._modifiedRecords.Keys);
				if (!filter.HasFlag(GristRecordState.MarkedForDeletion))
					baseCandidates = baseCandidates.Except(source._deletedRecords);
			}
			else
			{
				baseCandidates = Enumerable.Empty<ulong>();
				if (filter.HasFlag(GristRecordState.Modified))
					baseCandidates = baseCandidates.Union(source._modifiedRecords.Keys).Except(source._deletedRecords);
				if (filter.HasFlag(GristRecordState.MarkedForDeletion))
					baseCandidates = baseCandidates.Union(source._deletedRecords);
			}

			_baseRecords = baseCandidates.ToArray();
			
			Debug.Assert(_total == (_searchNewRecords ? source._newRecords.Count + _baseRecords.Length : _baseRecords.Length), "Record enumerable candidates lenght doesn't match expected count");
		}

		private void SourceOnCollectionChanged(object _, NotifyCollectionChangedEventArgs e)
		{
		
		}

		public bool MoveNext()
		{
			if (++_pos >= _total)
			{
				Current = null!;
				return false;
			}

			if (_pos < _baseRecords.Length)
			{
				Current = new GristRecord(_source, _baseRecords[_pos]);
				return true;
			}
			
			if (_searchNewRecords)
			{
				Current = new GristRecord(_source, _pos - _baseRecords.Length);
				return true;
			}

			Debug.Fail("Record enumerable entered invalid state");
			return false;
		}

		public void Reset() => _pos = -1;

		public GristRecord Current { get; private set; } = null!;

		object? IEnumerator.Current => Current;

		public void Dispose()
		{
			_source.CollectionChanged -= SourceOnCollectionChanged;
		}
	}

	public GristTable Table { get; }
	public string TableId => Table.TableId;
	public GristDocument Document => Table.Document;
	public string DocumentId => Table.DocumentId;
	public int Count => _baseRowIndex.Count + _newRecords.Count;
	public IReadOnlyList<string> ColumnNames => _colNameByIdx.AsReadOnly();
	public IReadOnlyDictionary<string, int> ColumnMap => _colIdxByName.AsReadOnly();
	public IReadOnlyList<ulong> RowIds => _baseRowIds.AsReadOnly();

	private readonly int _colCount;
	private readonly HashSet<string> _colSet;
	private string[] _colNameByIdx;
	// private IReadOnlyDictionary<string, int> _colIdxByName;
#if NET8_0_OR_GREATER
	private FrozenDictionary<string, int> _colIdxByName;
#else
	private SortedList<string, int> _colIdxByName;
#endif

	private object?[] _baseRecords;
	private SortedList<ulong, int> _baseRowIndex = new();
	private List<ulong> _baseRowIds = new();
	private Dictionary<ulong, object?[]> _modifiedRecords = new();
	private List<object?[]> _newRecords = new();
	private HashSet<ulong> _deletedRecords = new();

	internal GristRecordSet(GristTable table)
	{
		Table = table;
		_colSet = new HashSet<string>(table.Columns.Keys);
		_colCount = _colSet.Count;
	}

	public int CountOf(GristRecordState filter)
	{
		if (filter == GristRecordState.All)
			return Count;
		int total = 0;
		if (filter.HasFlag(GristRecordState.New))
			total += _newRecords.Count;
		if (filter.HasFlag(GristRecordState.Existing))
			total += _baseRowIndex.Count - _modifiedRecords.Keys.Union(_deletedRecords).Count();
		if (filter.HasFlag(GristRecordState.Modified))
			total += _modifiedRecords.Keys.Except(_deletedRecords).Count();
		if (filter.HasFlag(GristRecordState.MarkedForDeletion))
			total += _deletedRecords.Count;
		return total;
	}

	public IDictionary<string, Action<IParseNode>> GetFieldDeserializers() => new Dictionary<string, Action<IParseNode>>
	{
		["records"] = ParseRecordsList,
	};

	internal ArraySegment<object?> GetRawRecord(int idx) =>
		new(_baseRecords, idx * _colCount, _colCount);

	public GristRecord GetRecord(ulong rowId) => new GristRecord(this, rowId);

	private void ParseRecordsList(IParseNode root)
	{
		List<UntypedObject> records = root.GetCollectionOfObjectValues(UntypedNode.CreateFromDiscriminatorValue).Cast<UntypedObject>().ToList();

		if (records.Count == 0) return;

		IList<string> colOrder = ((UntypedObject)records[0].GetValue()["fields"]).GetValue().Keys.ToList();
		int startAtIdx = 0;

		if (_colNameByIdx == null)
		{
			SetupColumns(colOrder);
			_baseRecords = new object[records.Count * _colCount];
			_baseRowIndex.Clear();
		}
		else
		{
			if (colOrder.Count != _colCount)
				throw new InvalidOperationException("Incoming record columns do not match column count");
			if (Enumerable.Range(0, _colCount).Select(i => _colNameByIdx[i] == colOrder[i]).Any(match => !match))
				throw new InvalidOperationException("Incoming record columns do not match established columns");
			startAtIdx = _baseRecords.Length;
			Array.Resize(ref _baseRecords, _baseRecords.Length + (records.Count * _colCount));
		}

		for (int row = 0; row < records.Count; row++)
		{
			IDictionary<string, UntypedNode> record = records[row].GetValue();
			ulong id = record["id"] switch
			{
				UntypedInteger ui => Convert.ToUInt64(ui.GetValue()),
				UntypedLong ul => Convert.ToUInt64(ul.GetValue()),
				UntypedDouble ud => Convert.ToUInt64(ud.GetValue()),
				_ => throw new InvalidOperationException($"Row ID in {record["id"].GetType().Name} object")
			};
			
			UntypedObject fields = record["fields"] as UntypedObject ??
			                   throw new InvalidOperationException($"{TableId}[{id}] has no fields?");
			int col = 0;
			foreach (UntypedNode field in fields.GetValue().Values)
			{
				_baseRecords[startAtIdx + (row * _colCount) + col++] = GristRecordParser.UnwrapValue(field, Document);
			}
			_baseRowIndex.Add(id, row);
			_baseRowIds.Add(id);
		}
	}

	private void SetupColumns(ICollection<string> names)
	{
		if (_colSet.Intersect(names).Count() != _colCount)
			throw new InvalidOperationException("Column mismatch when loading records");

		_colNameByIdx = new string[_colCount];
		names.CopyTo(_colNameByIdx, 0);

		Dictionary<string, int> mapping = new();
		for (int i = 0; i < _colCount; i++)
			mapping[_colNameByIdx[i]] = i;

#if NET8_0_OR_GREATER
		_colIdxByName = mapping.ToFrozenDictionary();
#else
		_colIdxByName = new SortedList<string, int>(mapping);
#endif
	}

	internal GristRecordSet ExpandSelf(IParseNode node) => this;

	public void Serialize(ISerializationWriter writer) => throw new NotImplementedException();

	public IEnumerator<GristRecord> GetEnumerator() => new GristRecordSetEnumerator(this);

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	
	public event NotifyCollectionChangedEventHandler? CollectionChanged;

	protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e) => CollectionChanged?.Invoke(this, e);
}
