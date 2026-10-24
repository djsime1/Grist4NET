using System.Collections;
using System.Text.Json;
using Grist4NET.Kiota.Docs.Item.Sql;
using Grist4NET.Kiota.Models;
using Microsoft.Kiota.Abstractions;

namespace Grist4NET;

/// <summary>
/// Base class for filters that limit which records get returned from a request.
/// See derived types (<see cref="GristCellValueFilter{T}"/> and <see cref="GristListContainsFilter{T}"/>) for usage examples.
/// </summary>
public abstract class GristRecordFilter(string columnId) : IEnumerable
{
	/// <summary>
	/// The column ID that will be filtered, only one filter can be specified per column.
	/// </summary>
	public string ColumnId { get; } = columnId;

	/// <summary>
	/// Compute what values will be provided to the record list request.
	/// </summary>
	/// <param name="table">The table this filter is being applied to.</param>
	/// <returns>A list of one or more values to be passed as filter criteria.</returns>
	protected internal abstract Task<IList> GetValues(GristTable table);

	/// <inheritdoc />
	public abstract IEnumerator GetEnumerator();
}

// TODO: Either constrain filter generics to structs or string, or add runtime type-checking/handling

/// <summary>
/// Filters a column to cells whose values are present within this filter list.
/// </summary>
/// <example>
/// To filter a string column where Last_name is Smith:
/// <code>
/// var nameFilter = new GristCellValueFilter&lt;string&gt;("Last_name") { "Smith" };
/// </code>
/// To filter a string/choice column where Item_size is Medium or Large:
/// <code>
/// public enum Size { Small, Medium, Large }
/// var sizeFilter = new GristCellValueFilter&lt;Size&gt;("Item_size") { Size.Medium, Size.Large };
/// </code>
/// To retrieve specific row ID's:
/// <code>
/// var rowIDs = new List&lt;uint&gt; { 1, 2, 3 };
/// var rowIDFilter = new GristCellValueFilter&lt;uint&gt;("Id", ids);
/// </code>
/// </example>
public sealed class GristCellValueFilter<T> : GristRecordFilter
{
	public GristCellValueFilter(string columnId, IEnumerable<T>? values = null)
		: base(columnId)
	{
		if (values != null)
			Values.AddRange(values);
	}

	public GristCellValueFilter(GristColumn column, IEnumerable<T>? values = null)
		: this(column.ColumnId, values) { }

	public List<T> Values { get; } = new();

	protected internal override Task<IList> GetValues(GristTable _) =>
		Task.FromResult<IList>(Values);

	public void Add(T value) => Values.Add(value);

	public override IEnumerator GetEnumerator() => Values.GetEnumerator();
}

/// <summary>
/// Filters a Choice List or Reference List column to cells that contain any value present within this filter list.
/// </summary>
/// <example>
/// To filter a choice list column where Color_tags contains Red or Blue:
/// <code>
/// public enum Color { Red, Yellow, Green, Blue }
/// var colorFilter = new GristListContainsFilter&lt;Color&gt;("Color_tag") { Color.Red, Color.Blue };
/// </code>
/// For reference list columns, use the row ID of the referenced record.
/// </example>
public sealed class GristListContainsFilter<T> : GristRecordFilter
{
	public GristListContainsFilter(string columnId, IEnumerable<T>? values = null)
		: base(columnId)
	{
		if (values != null)
			Values.AddRange(values);
	}

	public GristListContainsFilter(GristColumn column, IEnumerable<T>? values = null)
		: this(column.ColumnId, values) { }

	public List<T> Values { get; } = new();

	protected internal override async Task<IList> GetValues(GristTable table)
	{
		string valArray = JsonSerializer.Serialize(Values);
		string sqlQuery = $"""
			SELECT DISTINCT t.{ColumnId} AS v
			FROM {table.TableId} t
			WHERE EXISTS (
			    SELECT 1
			    FROM json_each(t.{ColumnId}) AS a
			    JOIN json_each(?) AS b ON a.value = b.value
			);
			""";
		SqlPostRequestBody sqlReqBody = new()
		{
			Sql = sqlQuery,
			Args = [new() { String = valArray }],
		};
		SqlResultSet sqlResp;
		try
		{
			sqlResp = (await table.Api.Docs[table.DocumentId].Sql.PostAsync(sqlReqBody))!;
		}
		catch (ApiException e)
		{
			RequestInformation req = table
				.Api.Docs[table.DocumentId]
				.Sql.ToPostRequestInformation(sqlReqBody);
			throw new GristApiException("List contents query", e, req);
		}

		return sqlResp
			.Records!.Select(r => r.Fields!.AdditionalData["v"])
			.Cast<string>()
			.Select(j => JsonSerializer.Deserialize<JsonElement>(j))
			.ToList();
	}

	public void Add(T value) => Values.Add(value);

	public override IEnumerator GetEnumerator() => Values.GetEnumerator();
}
