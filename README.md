<table>
<tr>
<td>
<img src="media/icon.svg" align="center" width="128">
</td>
<td>

# [Grist4NET](https://g4n.dj1.dev)

*A Kiota client and wrapper for the [Grist](https://getgrist.com/) API.*  
Currently capable of fetching Grist documents and their data, as well as binding said data to class properties.
</td>
</tr>
</table>

> [!IMPORTANT]
> This side-project is under active development (unless the last commit was over two months ago...) and will not have
a stable API until a 1.0.0 release is made. It's also missing documentation for most members, but you can get the gist of how to use the
wrapper by looking at the samples directory.

> [!NOTE]
> **Grist** is a trademark of *Grist Labs, Inc.*  
> **Grist4NET** is not affiliated with or endorsed by *Grist Labs, Inc.*

---

## Front matter
Grist4NET aims to provide an easy-to-use API wrapper for Grist documents. As of now, the scope is limited to only
reading document properties, tables, and rows. To that extent, records can also be mapped to POCO properties in order to
provide strongly-typed access to data.

## Installation
NuGet packages are available on [nuget.org](https://www.nuget.org/packages?q=Grist4NET) and [Codeberg](https://codeberg.org/dj1/Grist4NET/packages):

<table>
<thead>
<tr>
<th>Source</th>
<th colspan="2">Package</th>
</tr>
</thead>
<tbody>
<tr>
<td>NuGet.org</td>
<td><a href="https://www.nuget.org/packages/Grist4NET">Grist4NET</a></td>
<td><a href="https://www.nuget.org/packages/Grist4NET.Kiota">Grist4NET.Kiota</a></td>
</tr>
<tr>
<td>Codeberg</td>
<td><a href="https://codeberg.org/dj1/-/packages/nuget/grist4net">Grist4NET</a></td>
<td><a href="https://codeberg.org/dj1/-/packages/nuget/grist4net.kiota">Grist4NET.Kiota</a></td>
</tr>
</tbody>
</table>

`Grist4NET.Kiota` contains the Kiota-generated API client, while `Grist4NET` uses that client to provide easy-to-use
wrapper types and objects. All wrapper types can be loaded `using Grist4NET;` and have names prefixed with `Grist*`.

## Quick start
The basis of interacting with a Grist instance starts with a `GristConnection`. You'll need to set up an
[API key](https://support.getgrist.com/rest-api/#api-keys), and optionally specify a base URL for the API (defaults to 
`https://docs.getgrist.com/api`, the official SAAS offering of Grist).

With a `GristConnection`, you can retrieve documents. Documents and tables are cached, but can be manually re-fetched by
calling the `Refresh()` method on `GristDocument` or `GristTable`.

```cs
var grist = new GristConnection("<API-KEY-GOES-HERE>");
var doc = await grist.GetDocument("<DOCUMENT-ID-GOES-HERE>");
```

Once you have a `GristDocument`, you can peek at its tables with the `GristDocument.Tables` property. This view only
provides basic table metadata, but no way to interact with them. To get a `GristTable` object that can actually be
worked with, use `GristDocument.GetTable(...)`. With a `GristTable`, you can count and retrieve records.

```cs
var table = await doc.GetTable("<TABLE-ID-GOES-HERE>");
var rowcount = await table.CountRecords();
```

To get unstructured (dictionary) record data out of a table, call `GristTable.GetRecords(...)` or
`GristTable.GetRecordsWithId(...)`, optionally specifying a limit to the number of rows retrieved. Records can also be
filtered by column values using one or more `GristRecordFilter`'s.

```cs
// Fetch up to 5 rows where the value of the "Some_column"-ID column is "Value A" or "Value B"
var records = await table.GetRecordsWithId(5, new GristRecordFilter<string>("Some_column") {"Value A", "Value B"});
// The returned dictionary keys will be the row ID's, and values will be dictionaries.
// GristTable.GetRecords(...) is equivalent to GristTable.GetRecordsWithId(...).Values
```

## Record binding
Instead of reading data from untyped dictionaries, Grist4NET can also map record data to DTO's. Instead of using
`GristTable.GetRecords[WithId](...)`, use `GristTable.BindRecords[WithId]<T>(...)` to return a collection of objects
with type T. The library will attempt to automatically map/convert columns to public-settable properties.

Column/Cell types map to the following property types:
- Text: `string` or Enum
- Numeric: Any unmanaged numeric value type
- Int: Same as Numeric
- Bool: `bool`
- Date: `DateOnly` in .NET 8.0+, `DateTime` in .NET Standard
- DateTime: `DateTimeOffset` in .NET 8.0+, `DateTime` in .NET Standard
- Choice: `string` or Enum
- Choice List: Any type that implements `ICollection<T>`, where T is a `string` or Enum
- Reference: `GristReference` or `GristReference<T>`*
- Reference List: `GristReferenceList` or `GristReferenceList<T>`*
- Attachment: `GristAttachmentList`

> [!CAUTION]
> \****`GristReference[List]<T>` is not yet implemented!***

Columns ID's are matched to property names. To manually specify the column ID for a property, use the
`[GristColumnId("Some_column")]` attribute. To exclude a property from mapping, apply the `[GristIgnore]` attribute.

Likewise, Enum names are matched to choices. You should use the `[GristChoice("Choice value")]` attribute on the enum
member to change the mapping, but `[EnumMember(Value = "...")]` also works for compatibility.

> [!TIP]
> Check out the samples directory for complete examples.
