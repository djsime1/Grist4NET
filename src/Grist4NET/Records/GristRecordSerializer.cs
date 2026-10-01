using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Grist4NET;

/*
 * may the powers that be boon your soul should you end up in this jungle
 *
 * Deserialization flow:
 * - GristRecordSerializer: Find intersection of record and type properties
 *   - GristRecordParser: Unwrap typed cell values & parse into basic C# types
 *   - GristValueConverter: When the parsed and property types aren't compatible, this figures out how to make it work
 *     - GristChoiceConverter: Translates string choice option into an enum
 */

/// <summary>
/// Parses Grist JSON records and binds them to the properties of a POCO.
/// </summary>
/// <typeparam name="T">Type that properties will be bound to</typeparam>
/// <seealso cref="GristColumnIdAttribute"/>
/// <seealso cref="GristIgnoreAttribute"/>
public static class GristRecordSerializer<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
>
{
	internal class BindingPoint
	{
		internal required PropertyInfo Property { get; init; }
		internal required Type PropertyType { get; init; }
		internal required bool IsNullable { get; init; }
	}

	private static readonly Dictionary<string, BindingPoint> BindingTargets;

	static GristRecordSerializer()
	{
		BindingTargets = typeof(T)
			.GetProperties(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public)
			.Where(p => p.GetCustomAttribute<GristIgnoreAttribute>() == null)
			.ToDictionary(
				p => p.GetCustomAttribute<GristColumnIdAttribute>()?.ColumnId ?? p.Name,
				p => new BindingPoint
				{
					Property = p,
					PropertyType = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType,
					IsNullable = Nullable.GetUnderlyingType(p.PropertyType) != null,
				}
			);
	}

	/// <summary>
	/// Tests if a particular table can be deserialized into the properties of a particular type
	/// </summary>
	/// <param name="table">The columns that will be checked</param>
	/// <param name="throwOnIncompatible">If true, this method will throw when deserialization would fail</param>
	public static bool CanDeserialize(GristTable table, bool throwOnIncompatible = false)
	{
		IEnumerable<string> fields = BindingTargets.Keys.Intersect(table.Columns.Keys);
		bool compatible = true;

		foreach (string key in fields)
		{
			BindingPoint binding = BindingTargets[key];

			compatible &= binding.Property.CanWrite;
			if (throwOnIncompatible && !compatible)
				throw new InvalidOperationException(
					$"Can not set value of non-writable property {typeof(T).Name}.{binding.Property.Name}"
				);

			Type propertyType = binding.PropertyType;
			compatible &= !propertyType.IsAbstract && !propertyType.IsInterface;
			if (throwOnIncompatible && !compatible)
				throw new InvalidOperationException(
					$"Can not set value of abstract/interface-typed property {typeof(T).Name}.{binding.Property.Name}"
				);

			// TODO: Type compatibility checking
		}

		return compatible;
	}

	/// <summary>
	/// Attempt to deserialize the fields of a Grist record into a given DTO
	/// Automatically handles unwrapping, parsing, and conversion
	/// </summary>
	/// <seealso cref="CanDeserialize"/>
	public static void Deserialize(IDictionary<string, object?> fields, T instance)
	{
		fields = fields.Where(i => BindingTargets.ContainsKey(i.Key)).ToDictionary();

		foreach ((string key, object? value) in fields)
		{
			BindingPoint binding = BindingTargets[key];
			if (value is null or "")
				if (binding.IsNullable)
					continue;
				else
					throw new InvalidOperationException(
						$"Got null/empty value in column {key}, but mapped property is not nullable."
					);

			Type valueType = value.GetType();
			if (binding.PropertyType.IsAssignableFrom(valueType))
			{
				binding.Property.SetValue(instance, value);
				continue;
			}

			object convertedValue = GristValueConverter.ConvertValue(value, binding.PropertyType);
			switch (convertedValue)
			{
				case IGristReference:
				case IGristReferenceList:
					// This only occurs when it's the generic type
					// TODO: Implement recursive binding for GristReference(List)<>
					throw new NotImplementedException();
			}
			// TODO: Add options for handling un-convertable/null values (Assign null, throw, etc)
			binding.Property.SetValue(instance, convertedValue);
		}
	}
}
