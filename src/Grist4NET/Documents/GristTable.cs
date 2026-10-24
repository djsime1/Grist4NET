using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Grist4NET.Kiota;
using Grist4NET.Kiota.Docs.Item.Sql;
using Grist4NET.Kiota.Models;
using Microsoft.Kiota.Abstractions;

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

	/// <summary>
	/// Internal immutable column identifier used by Grist.
	/// </summary>
	public int TableRef { get; }

	/// <summary>
	/// Mutable table identifier.
	/// </summary>
	public string TableId { get; internal set; }

	/// <summary>
	/// A deprecated Grist feature, on-demand tables bypass (and therefore, miss out on) most of Grist's data engine.
	/// For more information, see <see href="https://support.getgrist.com/on-demand-tables/"/>
	/// </summary>
	public bool OnDemand { get; internal set; }
}

/// <summary>
/// A table within a Grist document. Provides methods to manipulate data.
/// </summary>
public class GristTable : GristTableMetadata
{
	internal readonly GristKiotaClient Api;
	private readonly Dictionary<string, GristColumn> _columnCache = new();

	/// <summary>
	/// The Grist document containing this table.
	/// </summary>
	public GristDocument Document { get; }

	/// <summary>
	/// The full ID of the document containing this table.
	/// </summary>
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

	/// <summary>
	/// A map of column ID's to metadata objects. For choice and reference columns, these can be cast to
	/// <see cref="GristChoiceColumn"/> and <see cref="GristReferenceColumn"/> respectively to get their parameters.
	/// </summary>
	public IReadOnlyDictionary<string, GristColumn> Columns => _columnCache.AsReadOnly();

	internal async Task<GristTable> Initialize(Dictionary<int, GristTable>? cache = null)
	{
		await Refresh();
		cache?.Add(TableRef, this);
		return this;
	}

	/// <summary>
	/// Retrieves the table's current structure from Grist and syncs it with this instance.
	/// </summary>
	public async Task Refresh()
	{
		ColumnsList colsResponse;
		try
		{
			// If Document/TableId has changed, we're SOL anyway! idc
			colsResponse = (await Api.Docs[DocumentId].Tables[TableId].Columns.GetAsync())!;
		}
		catch (ApiException e)
		{
			RequestInformation req = Api.Docs[DocumentId]
				.Tables[TableId]
				.Columns.ToGetRequestInformation();
			throw new GristApiException("Table columns", e, req);
		}

		// TODO: Update metadata instead of creating new every time
		_columnCache.Clear();
		foreach (ColumnsList_columns column in colsResponse.Columns!)
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
	}

	/// <summary>
	/// Counts the total number of records within a table without retrieving them using an SQL query.
	/// </summary>
	public async Task<ulong> CountRecords()
	{
		SqlResultSet countResp;
		SqlPostRequestBody countReqBody = new()
		{
			Sql = $"SELECT count(*) AS count FROM '{TableId}'",
		};
		try
		{
			countResp = (await Api.Docs[DocumentId].Sql.PostAsync(countReqBody))!;
		}
		catch (ApiException e)
		{
			RequestInformation req = Api.Docs[DocumentId]
				.Sql.ToPostRequestInformation(countReqBody);
			throw new GristApiException("Count rows", e, req);
		}
		return Convert.ToUInt64(countResp.Records![0].Fields!.AdditionalData["count"]);
	}

	// TODO: Add parameter for sort

	public async Task<IDictionary<ulong, IDictionary<string, object?>>> GetRecordsWithId(
		params GristRecordFilter[] filters
	) => await GetRecordsWithId(0, filters);

	public async Task<IDictionary<ulong, IDictionary<string, object?>>> GetRecordsWithId(
		ulong limit,
		params GristRecordFilter[] filters
	) => await GetRecordsInternal(limit, filters).ToDictionaryAsync();

	public async Task<ICollection<IDictionary<string, object?>>> GetRecords(
		params GristRecordFilter[] filters
	) => await GetRecords(0, filters);

	public async Task<ICollection<IDictionary<string, object?>>> GetRecords(
		ulong limit,
		params GristRecordFilter[] filters
	) => (await GetRecordsInternal(limit, filters).ToDictionaryAsync()).Values;

	public async Task<IDictionary<ulong, T>> BindRecordsWithId<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(params GristRecordFilter[] filters)
		where T : new() => await BindRecordsWithId<T>(0, filters);

	public async Task<IDictionary<ulong, T>> BindRecordsWithId<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(ulong limit, params GristRecordFilter[] filters)
		where T : new() => await BindRecordsWithId<T>(() => new T(), 0, filters);

	public async Task<IDictionary<ulong, T>> BindRecordsWithId<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(Func<T> instantiator, ulong limit, params GristRecordFilter[] filters) =>
		await BindRecordsInternal(instantiator, GetRecordsInternal(limit, filters));

	public async Task<ICollection<T>> BindRecords<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(params GristRecordFilter[] filters)
		where T : new() => await BindRecords<T>(0, filters);

	public async Task<ICollection<T>> BindRecords<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(ulong limit, params GristRecordFilter[] filters)
		where T : new() => await BindRecords<T>(() => new T(), 0, filters);

	public async Task<ICollection<T>> BindRecords<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
	>(Func<T> instantiator, ulong limit, params GristRecordFilter[] filters) =>
		(await BindRecordsInternal(instantiator, GetRecordsInternal(limit, filters))).Values;

	private async IAsyncEnumerable<
		KeyValuePair<ulong, IDictionary<string, object?>>
	> GetRecordsInternal(ulong limit, GristRecordFilter[] filters)
	{
		Dictionary<string, object> filterDict = new();

		foreach (GristRecordFilter filter in filters)
		{
			string cid = filter.ColumnId;
			if (filterDict.ContainsKey(cid))
				throw new InvalidOperationException(
					$"Can't have more than one filter targeting the {cid} column."
				);
			filterDict[cid] = await filter.GetValues(this);
		}

		RecordsListParameters recordsReqBody = new()
		{
			Filter = new() { AdditionalData = filterDict },
			Limit = limit,
			CellFormat = RecordsListParameters_cellFormat.Typed,
		};
		RecordsList recordsResp;
		try
		{
			recordsResp = (
				await Api.Docs[DocumentId].Tables[TableId].Records.List.PostAsync(recordsReqBody)
			)!;
		}
		catch (ApiException e)
		{
			RequestInformation req = Api.Docs[DocumentId]
				.Tables[TableId]
				.Records.List.ToPostRequestInformation(recordsReqBody);
			throw new GristApiException("Records list", e, req);
		}

		foreach (RecordsList_records record in recordsResp.Records!)
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
