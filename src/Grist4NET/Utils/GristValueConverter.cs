using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Grist4NET;

internal static class GristValueConverter
{
	private static readonly Dictionary<Type, bool> CollectionTypeCache = new();
	private static readonly Dictionary<Type, bool> ParsableTypeCache = new();

	private static readonly MethodInfo TryParseChoiceIntoMethod =
		typeof(GristValueConverter).GetMethod(
			nameof(TryParseChoiceInto),
			BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly
		)!;

	private static bool TryParseChoiceInto<TEnum>(
		string choice,
		[MaybeNullWhen(false)] out TEnum value
	)
		where TEnum : struct, Enum => GristChoiceConverter<TEnum>.TryGetEnum(choice, out value);

	internal static object ConvertValue(object value, Type fieldType)
	{
		// TODO: Optimize by caching conversion Func<>
		switch (value)
		{
			case IConvertible when !fieldType.IsEnum:
				return Convert.ChangeType(value, fieldType);
			case string stringValue when fieldType.IsEnum:
				return ConvertEnum(stringValue, fieldType);
			case string stringValue when fieldType == typeof(Uri):
				return new Uri(stringValue);
			case GristReference when fieldType.IsAssignableTo(typeof(IGristReference)):
			case GristReferenceList when fieldType.IsAssignableTo(typeof(IGristReferenceList)):
				// Recursive binding handled higher in stack
				return value;
			#if NET8_0_OR_GREATER
			case DateTimeOffset dto when fieldType == typeof(DateTime):
				return dto.UtcDateTime;
			case string stringValue when IsParsableType(fieldType) && TryParseParsable(stringValue, fieldType, out object? parsedValue):
				return parsedValue!;
			#endif
		}

		if (
			value is IEnumerable list
			&& TryConvertCollection(list, fieldType, out IEnumerable? collection)
		)
			return collection;

		// TODO: Convert using implicit/explicit cast operators

		string message =
			fieldType.IsEnum
			|| (fieldType.Namespace?.StartsWith("System") ?? false)
			|| (fieldType.Namespace?.StartsWith(nameof(Grist4NET)) ?? false)
				? $"Can not convert {value.GetType()} to {fieldType}"
				: $"Can not convert {value.GetType()} to {fieldType} (Did you mean to use a GristReference<T> for recursive binding?)";

		throw new InvalidCastException(message);
	}

	internal static TValue ConvertValue<TValue>(object from) =>
		(TValue)ConvertValue(from, typeof(TValue));

	internal static object ConvertEnum(string choice, Type enumType)
	{
		object[] args = [choice, null!];
		bool parsed = (bool)
			TryParseChoiceIntoMethod.MakeGenericMethod(enumType).Invoke(null, args)!;
		return parsed
			? args[1]
			: throw new InvalidCastException(
				$"Unable to map choice \"{choice}\" to enum {enumType}"
			);
	}

	internal static bool TryConvertCollection(
		IEnumerable sourceCollection,
		Type targetType,
		[MaybeNullWhen(false)] out IEnumerable targetCollection
	)
	{
		targetCollection = null;

		if (targetType.IsInstanceOfType(sourceCollection))
		{
			targetCollection = sourceCollection;
			return true;
		}

		Type sourceType = sourceCollection.GetType();

		// TODO: Handle read-only collections target types
		if (
			targetType.IsAbstract
			|| targetType.IsInterface
			|| !IsCollectionType(sourceType)
			|| !IsCollectionType(targetType)
		)
			return false;

		targetCollection = (IEnumerable)Activator.CreateInstance(targetType)!;

		Type sourceGeneric = sourceType.GetGenericArguments()[0];
		Type targetGeneric = targetType.GetGenericArguments()[0];

		ConvertCollectionMethod
			.MakeGenericMethod(sourceGeneric, targetGeneric)
			.Invoke(null, [sourceCollection, targetCollection]);
		return true;
	}

	private static bool IsCollectionType(Type type)
	{
		bool isCollection;
		if (CollectionTypeCache.TryGetValue(type, out isCollection))
			return isCollection;

		isCollection = type.GetInterfaces()
			.Where(iftype => iftype.IsConstructedGenericType)
			.Any(iftype => iftype.GetGenericTypeDefinition() == typeof(ICollection<>));

		CollectionTypeCache[type] = isCollection;
		return isCollection;
	}

	private static readonly MethodInfo ConvertCollectionMethod =
		typeof(GristValueConverter).GetMethod(
			nameof(ConvertCollection),
			BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly
		) ?? throw new InvalidOperationException();

	internal static void ConvertCollection<TSource, TTarget>(
		IEnumerable<TSource> source,
		ICollection<TTarget> target
	)
	{
		foreach (TSource sourceItem in source)
		{
			if (sourceItem is TTarget targetItem)
				target.Add(targetItem);
			else
				target.Add(ConvertValue<TTarget>(sourceItem!));
		}
	}
	
#if NET8_0_OR_GREATER
	private static bool IsParsableType(Type type)
	{
		bool isParsable;
		if (ParsableTypeCache.TryGetValue(type, out isParsable))
			return isParsable;

		isParsable = type.GetInterfaces()
			.Where(iftype => iftype.IsConstructedGenericType)
			.Any(iftype => iftype.GetGenericTypeDefinition() == typeof(IParsable<>));

		ParsableTypeCache[type] = isParsable;
		return isParsable;
	}
	
	internal static bool TryParseParsable(
		string sourceString,
		Type targetType,
		[MaybeNullWhen(false)] out object? parsedValue
	)
	{
		MethodInfo tryParseMethod = targetType.GetMethod(nameof(IParsable<>.TryParse),
			BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly) ?? throw new InvalidOperationException($"Couldn't find TryParse method on {targetType}");
		object[] args = [sourceString, null!, null!];
		bool parsedResult = (bool)
			TryParseChoiceIntoMethod.MakeGenericMethod(targetType).Invoke(null, args)!;
		parsedValue = args[2];
		return parsedResult;
	}
#endif
}
