using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Serialization;

namespace Grist4NET;

/// <summary>
/// Translates Grist choice options to Enums
/// </summary>
/// <typeparam name="TEnum">Enum type</typeparam>
/// <seealso cref="GristChoiceAttribute"/>
/// <seealso cref="EnumMemberAttribute"/>
public static class GristChoiceConverter<TEnum>
	where TEnum : struct, Enum
{
	private static readonly Dictionary<string, TEnum> EnumNameMap = new();

	static GristChoiceConverter()
	{
		foreach (
			(string eName, TEnum eValue) in Enum.GetNames<TEnum>().Zip(Enum.GetValues<TEnum>())
		)
		{
			EnumNameMap.Add(eName, eValue);
			MemberInfo member = typeof(TEnum).GetMember(eName)[0];
			if (member.GetCustomAttribute<GristChoiceAttribute>() is { } choiceAttribute)
				EnumNameMap.Add(choiceAttribute.Choice, eValue);
			if (
				member.GetCustomAttribute<EnumMemberAttribute>() is
				{ IsValueSetExplicitly: true } memberAttribute
			)
				EnumNameMap.Add(memberAttribute.Value!, eValue);
		}
	}

	public static bool HasEnum(string choice) => EnumNameMap.ContainsKey(choice);

	public static TEnum GetEnum(string choice) => EnumNameMap[choice];

	public static bool TryGetEnum(string choice, [MaybeNullWhen(false)] out TEnum value) =>
		EnumNameMap.TryGetValue(choice, out value);
}
