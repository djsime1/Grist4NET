using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Grist4NET.Kiota;
using Grist4NET.Kiota.Docs.Item.Tables.Item.Records;
using Grist4NET.Kiota.Models;

namespace Grist4NET;

/// <summary>
/// Provides info about a table, but can not be used to read/write data.
/// Call <see cref="GristDocument.GetTable"/> to get a workable <see cref="GristTable"/>.
/// </summary>
public class GristTableMetadata
{
	internal GristTableMetadata(int tableRef, string tableId, bool onDemand)
	{
		TableRef = tableRef;
		TableId = tableId;
		OnDemand = onDemand;
	}

	internal GristTableMetadata(TablesList_tables source)
		: this(
			source.Fields!.TableRef ?? throw new ArgumentNullException(nameof(source)),
			source.Id ?? throw new ArgumentNullException(nameof(source)),
			source.Fields!.OnDemand ?? throw new ArgumentNullException(nameof(source))
		) { }

	internal GristTableMetadata(GristTableMetadata source)
		: this(source.TableRef, source.TableId, source.OnDemand) { }

	public int TableRef { get; }
	public string TableId { get; internal set; }
	public bool OnDemand { get; internal set; }
}

/// <summary>
/// A table within a Grist document. Provides methods to manipulate data.
/// </summary>
public class GristTable : GristTableMetadata
{
	internal readonly GristKiotaClient Api;
	private readonly Dictionary<string, GristColumn> _columnCache = new();
	public GristDocument Document { get; }
	public string DocumentId => Document.DocumentId;

	internal GristTable(
		GristTableMetadata source,
		GristDocument document,
		GristKiotaClient apiClient
	)
		: base(source)
	{
		Api = apiClient;
		Document = document;
	}

	public IReadOnlyDictionary<string, GristColumn> Columns => _columnCache.AsReadOnly();

	internal async Task<GristTable> Initialize(Dictionary<int, GristTable>? cache = null)
	{
		await Refresh();
		cache?.Add(TableRef, this);
		return this;
	}

	/// <summary>
	/// Retrieves the table's current structure from Grist and syncs it with this instance
	/// </summary>
	public async Task Refresh()
	{
		// If Document/TableId has changed, we're SOL anyway! idc
		Task<ColumnsList?> colsRequest = Api.Docs[DocumentId].Tables[TableId].Columns.GetAsync();
		Task<ulong> countRequest = CountRecords();

		ColumnsList? colsResponse = await colsRequest;
		// TODO: Update metadata instead of creating new every time
		_columnCache.Clear();
		foreach (ColumnsList_columns column in colsResponse!.Columns!)
		{
			GristColumn wrapper;
			GristColumnType type = GristColumnTypeHelpers.GetWrapperEnum(
				column.Fields!.Type!,
				out _
			);
			switch (type.GetCategory())
			{
				case GristColumnTypeCategory.Value:
				case GristColumnTypeCategory.Date:
					wrapper = new GristColumn(column);
					break;
				case GristColumnTypeCategory.Choice:
					JsonDocument json = JsonDocument.Parse(column.Fields.WidgetOptions!);
					JsonElement.ArrayEnumerator choices = json
						.RootElement.GetProperty("choices")
						.EnumerateArray();
					wrapper = new GristChoiceColumn(
						column,
						choices.Select(element => element.GetString()!).ToList()
					);
					break;
				case GristColumnTypeCategory.Reference:
					wrapper = new GristReferenceColumn(column);
					break;
				case GristColumnTypeCategory.Unknown:
					wrapper = new GristColumn(column);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			_columnCache.Add(column.Id!, wrapper);
		}

		await countRequest;
	}

	/// <summary>
	/// Counts the total number of records within a table without retrieving them using an SQL query
	/// </summary>
	public async Task<ulong> CountRecords()
	{
		SqlResultSet? countResp = await Api.Docs[DocumentId]
			.Sql.GetAsync(
				// "sql injection!!!!!1" boo hoo TableId can't be arbitrarily set w/o reflection, GetTable throws if it wasn't discovered in the document
				req => req.QueryParameters.Q = $"SELECT count(*) AS count FROM '{TableId}'"
			);
		return Convert.ToUInt64(countResp!.Records![0].Fields!.AdditionalData["count"]);
	}

	public async Task<IDictionary<ulong, IDictionary<string, object?>>> GetRecordsWithId(
		params IGristRecordFilter[] filters
	) => await GetRecordsWithId(0, filters);

	public async Task<IDictionary<ulong, IDictionary<string, object?>>> GetRecordsWithId(
		ulong limit,
		params IGristRecordFilter[] filters
	) => await GetRecordsInternal(limit, filters).ToDictionaryAsync();

	public async Task<ICollection<IDictionary<string, object?>>> GetRecords(
		params IGristRecordFilter[] filters
	) => await GetRecords(0, filters);

	public async Task<ICollection<IDictionary<string, object?>>> GetRecords(
		ulong limit,
		params IGristRecordFilter[] filters
	) => (await GetRecordsInternal(limit, filters).ToDictionaryAsync()).Values;

	public async Task<IDictionary<ulong, T>> BindRecordsWithId<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(params IGristRecordFilter[] filters)
		where T : new() => await BindRecordsWithId<T>(0, filters);

	public async Task<IDictionary<ulong, T>> BindRecordsWithId<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(ulong limit, params IGristRecordFilter[] filters)
		where T : new() => await BindRecordsWithId<T>(() => new T(), 0, filters);

	public async Task<IDictionary<ulong, T>> BindRecordsWithId<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(Func<T> instantiator, ulong limit, params IGristRecordFilter[] filters) =>
		await BindRecordsInternal(instantiator, GetRecordsInternal(limit, filters));

	public async Task<ICollection<T>> BindRecords<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(params IGristRecordFilter[] filters)
		where T : new() => await BindRecords<T>(0, filters);

	public async Task<ICollection<T>> BindRecords<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(ulong limit, params IGristRecordFilter[] filters)
		where T : new() => await BindRecords<T>(() => new T(), 0, filters);

	public async Task<ICollection<T>> BindRecords<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(Func<T> instantiator, ulong limit, params IGristRecordFilter[] filters) =>
		(await BindRecordsInternal(instantiator, GetRecordsInternal(limit, filters))).Values;

	private async IAsyncEnumerable<
		KeyValuePair<ulong, IDictionary<string, object?>>
	> GetRecordsInternal(ulong limit, IGristRecordFilter[] filters)
	{
		Dictionary<string, IEnumerable<object>> filterDict = filters.ToDictionary(
			i => i.ColumnId,
			i => i.GetValues().Cast<object>()
		);
		string filterJson = JsonSerializer.Serialize(filterDict);
		RecordsList? resp = await Api.Docs[DocumentId]
			.Tables[TableId]
			.Records.GetAsync(req =>
			{
				req.QueryParameters.Filter = filterJson;
				req.QueryParameters.Limit = limit;
				req.QueryParameters.CellFormat = GetCellFormatQueryParameterType.Typed;
			});

		foreach (RecordsList_records record in resp!.Records!)
		{
			ulong id = Convert.ToUInt64(record.Id);
			IDictionary<string, object?> value = record.Fields!.AdditionalData;
			GristRecordParser.UnwrapFields(value, Document);
			yield return new KeyValuePair<ulong, IDictionary<string, object?>>(id, value);
		}
	}

	private async Task<IDictionary<ulong, T>> BindRecordsInternal<T>(
		Func<T> instantiator,
		IAsyncEnumerable<KeyValuePair<ulong, IDictionary<string, object?>>> records
	)
	{
		GristRecordSerializer<T>.CanDeserialize(this, true);
		Dictionary<ulong, T> data = new();

		await foreach (KeyValuePair<ulong, IDictionary<string, object?>> record in records)
		{
			T value = instantiator.Invoke();
			GristRecordSerializer<T>.Deserialize(record.Value, value);
			data.Add(record.Key, value);
		}

		return data;
	}
}
