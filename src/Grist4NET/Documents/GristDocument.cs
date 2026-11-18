using Grist4NET.Kiota;
using Grist4NET.Kiota.Models;
using Microsoft.Kiota.Abstractions;

namespace Grist4NET;

/// <summary>
/// A Grist document, which contains tables.
/// </summary>
/// <seealso cref="GristConnection.GetDocument"/>
public class GristDocument
{
	internal readonly GristKiotaClient Api;
	private readonly Dictionary<int, GristTable> _tableCache = new();
	private readonly SemaphoreSlim _getTableSemaphore = new(1, 1);
	private readonly Dictionary<ulong, GristAttachment> _attachmentCache = new();
	private readonly SemaphoreSlim _getAttachmentSemaphore = new(1, 1);

	// TableId is more like a slug and can change, but used for API operations
	// TableRef is internal PK used by Grist
	// What if TableId gets changed? Lookup by ID, but cache by ref
	private readonly Dictionary<string, GristTableMetadata> _tableMetadata = new();

	internal GristDocument(string documentId, GristKiotaClient apiClient)
	{
		Api = apiClient;
		DocumentId = documentId;
	}

	/// <summary>
	/// Full document ID, used in API requests.
	/// </summary>
	public string DocumentId { get; }

	/// <summary>
	/// Truncated document ID, used in front-end URL.
	/// </summary>
	public string ShortDocumentId { get; internal set; }

	/// <summary>
	/// The name assigned to the document, as it appears in the UI.
	/// </summary>
	public string Name { get; internal set; }

	/// <summary>
	/// When this document was initially created.
	/// </summary>
	public DateTime CreatedAt { get; internal set; }

	/// <summary>
	/// When this document was last modified.
	/// </summary>
	public DateTime UpdatedAt { get; internal set; }

	/// <seealso cref="GristDocument.GetTable"/>
	public IReadOnlyDictionary<string, GristTableMetadata> Tables => _tableMetadata.AsReadOnly();

	internal async Task<GristDocument> Initialize(Dictionary<string, GristDocument>? cache = null)
	{
		await Refresh();
		cache?.Add(DocumentId, this);
		return this;
	}

	/// <summary>
	/// Retrieves the document's current structure from Grist and syncs it with this instance.
	/// </summary>
	public async Task Refresh()
	{
		DocWithWorkspace doc;
		try
		{
			doc = (await Api.Docs[DocumentId].GetAsync())!;
		}
		catch (ApiException e)
		{
			RequestInformation req = Api.Docs[DocumentId].ToGetRequestInformation();
			throw new GristApiException("Document metadata", e, req);
		}

		ShortDocumentId = doc.UrlId!;
		Name = doc.Name!;
		CreatedAt = (DateTime)doc.AdditionalData["createdAt"];
		UpdatedAt = (DateTime)doc.AdditionalData["updatedAt"];

		TablesList tables;
		try
		{
			tables = (await Api.Docs[DocumentId].Tables.GetAsync())!;
		}
		catch (ApiException e)
		{
			RequestInformation req = Api.Docs[DocumentId].Tables.ToGetRequestInformation();
			throw new GristApiException("Document tables", e, req);
		}

		if (tables.Tables!.Count == 0)
		{
			_tableMetadata.Clear();
			return;
		}

		if (_tableMetadata.Count == 0)
			foreach (TablesList_tables table in tables.Tables!)
				_tableMetadata[table.Id!] = new GristTableMetadata(table);
		else // this is probably overkill
			lock (_tableMetadata)
			{
				Dictionary<string, GristTableMetadata> temp = _tableMetadata.ToDictionary();
				_tableMetadata.Clear();
				List<int> tableRefs = tables
					.Tables!.Select(table => (int)table.Fields!.TableRef!)
					.ToList();
				IEnumerable<GristTableMetadata> toUpdate = temp.Values.Where(table =>
					!tableRefs.Contains(table.TableRef)
				);
				tableRefs = temp.Values.Select(table => table.TableRef).ToList();
				IEnumerable<TablesList_tables> toAdd = tables.Tables!.Where(table =>
					!tableRefs.Contains((int)table.Fields!.TableRef!)
				);
				Dictionary<int, TablesList_tables> refMapping = tables.Tables!.ToDictionary(
					x => (int)x.Fields!.TableRef!,
					x => x
				);
				foreach (GristTableMetadata tableMeta in toUpdate)
				{
					TablesList_tables table = refMapping[tableMeta.TableRef];
					tableMeta.TableId = table.Id!;
					tableMeta.IsOnDemand = (bool)table.Fields!.OnDemand!;
					// TODO: Update column metadata
					_tableMetadata[tableMeta.TableId] = tableMeta;
				}

				foreach (TablesList_tables table in toAdd)
				{
					GristTableMetadata tableMeta = new(table);
					_tableMetadata[tableMeta.TableId] = tableMeta;
				}
			}
	}

	/// <summary>
	/// Fetches a table by its ID, returns a wrapper object that can read and manipulate data.
	/// </summary>
	public async Task<GristTable> GetTable(string tableId)
	{
		await _getTableSemaphore.WaitAsync();
		try
		{
			GristTableMetadata meta = _tableMetadata[tableId];
			return _tableCache.TryGetValue(meta.TableRef, out GristTable? table)
				? table
				: await new GristTable(meta, this, Api).Initialize(_tableCache);
		}
		finally
		{
			_getTableSemaphore.Release();
		}
	}

	/// <summary>
	/// Fetches the metadata of an attachment by its numeric ID; The returned object can be used to download it.
	/// </summary>
	public async Task<GristAttachment> GetAttachment(ulong attachmentRef)
	{
		await _getAttachmentSemaphore.WaitAsync();
		try
		{
			return _attachmentCache.TryGetValue(attachmentRef, out GristAttachment? attachment)
				? attachment
				: await new GristAttachment(attachmentRef, this, Api).Initialize(_attachmentCache);
		}
		finally
		{
			_getTableSemaphore.Release();
		}
	}
}
