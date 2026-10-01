using Microsoft.Kiota.Abstractions.Serialization;

namespace Grist4NET;

/// <summary>
/// Unwraps and parses typed cells in API responses
/// </summary>
public static class GristRecordParser
{
	internal const string ATTACHMENT_TABLE_ID = "_grist_Attachments";

	/// <summary>
	/// Modifies the supplied dict in-place by replacing UntypedArray's with wrapper types
	/// </summary>
	/// <param name="fields">Dictionary that will be modified</param>
	/// <param name="document">For resolving references/attachments</param>
	public static void UnwrapFields(IDictionary<string, object?> fields, GristDocument document)
	{
		foreach ((string key, object? value) in fields.Where(field => field.Value is UntypedArray))
			fields[key] = UnwrapValue((value as UntypedArray)!, document);
	}

	/// <summary>
	/// Parses a single typed cell array
	/// </summary>
	/// <param name="array">The typed cell value</param>
	/// <param name="document">For resolving references/attachments</param>
	public static object? UnwrapValue(UntypedArray array, GristDocument document)
	{
		List<UntypedNode> nodes = array.GetValue().ToList();
		string typeCode = (nodes[0] as UntypedString)!.GetValue()!;
		GristCellType typeEnum = GristCellTypeHelpers.GetWrapperEnum(typeCode);
		nodes.RemoveAt(0);

		int num;
		string str;

		switch (typeEnum)
		{
			case GristCellType.List:
				return ParseStringList(nodes);

			case GristCellType.DateTime:
				num = (nodes[0] as UntypedInteger)!.GetValue();
				str = (nodes[1] as UntypedString)!.GetValue()!;
				return ParseDateTimeZone(num, str);

			case GristCellType.Date:
				num = (nodes[0] as UntypedInteger)!.GetValue();
				return ParseDate(num);

			case GristCellType.Reference:
				str = (nodes[0] as UntypedString)!.GetValue()!;
				num = (nodes[1] as UntypedInteger)!.GetValue();
				return new GristReference { TableId = str, RowId = Convert.ToUInt64(num) };

			case GristCellType.ReferenceList:
				str = (nodes[0] as UntypedString)!.GetValue()!;
				List<ulong> nums = ParseNumericList((nodes[1] as UntypedArray)!.GetValue());
				return str == ATTACHMENT_TABLE_ID
					? new GristAttachmentList
					{
						AttachmentIds = nums.AsReadOnly(),
						Document = document,
					}
					: new GristReferenceList { TableId = str, RowIds = nums.AsReadOnly() };

			case GristCellType.Exception:
			case GristCellType.Censored:
				return null;

			default:
				throw new InvalidOperationException(
					$"Can't parse cell with type {typeEnum} (type code '{typeCode}')"
				);
		}
	}

	private static List<string> ParseStringList(IEnumerable<UntypedNode> data) =>
		data.Select(i => (i as UntypedString)!.GetValue()).Cast<string>().ToList();

	private static List<ulong> ParseNumericList(IEnumerable<UntypedNode> data) =>
		data.Select(i => (i as UntypedInteger)!.GetValue()).Select(Convert.ToUInt64).ToList();

#if NET8_0_OR_GREATER
	private static DateTimeOffset ParseDateTimeZone(long epoch, string iana)
	{
		DateTimeOffset
			.FromUnixTimeSeconds(epoch)
			.Deconstruct(out DateOnly date, out TimeOnly time, out _);
		TimeZoneInfo tz =
			OperatingSystem.IsWindows()
			&& TimeZoneInfo.TryConvertIanaIdToWindowsId(iana, out string? id)
				? TimeZoneInfo.FindSystemTimeZoneById(id)
				: TimeZoneInfo.FindSystemTimeZoneById(iana);
		return new DateTimeOffset(date, time, tz.BaseUtcOffset);
	}

	private static DateOnly ParseDate(long epoch)
	{
		DateTimeOffset.FromUnixTimeSeconds(epoch).Deconstruct(out DateOnly date, out _, out _);
		return date;
	}
#else
	private static readonly string[] UTC_TZ =
	[
		"GMT",
		"Etc/GMT",
		"GMT+0",
		"Etc/GMT+0",
		"GMT-0",
		"Etc/GMT-0",
		"GMT0",
		"Etc/GMT0",
		"Greenwich",
		"Etc/Greenwich",
		"UCT",
		"Etc/UCT",
		"UTC",
		"Etc/UTC",
		"Universal",
		"Etc/Universal",
		"Zulu",
		"Etc/Zulu",
	];

	private static DateTime ParseDateTimeZone(long epoch, string iana)
	{
		DateTimeOffset dt = DateTimeOffset.FromUnixTimeSeconds(epoch);
		return UTC_TZ.Contains(iana) ? dt.UtcDateTime : dt.LocalDateTime;
	}

	private static DateTime ParseDate(long epoch) => DateTimeOffset.FromUnixTimeSeconds(epoch).Date;
#endif
}
