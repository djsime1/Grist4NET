using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

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
/// Parses Grist JSON records and binds them to the properties of a POCO/DTO.
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
					IsNullable =
						(
							p.PropertyType.IsValueType
							&& Nullable.GetUnderlyingType(p.PropertyType) != null
						)
#if NET8_0_OR_GREATER
						|| p.PropertyType.GetCustomAttribute<NullableAttribute>()?.NullableFlags[0]
							== 2
#elif NETSTANDARD2_1_OR_GREATER
						// Pretty sure this sort of usage isn't intended but whatever
						|| p.PropertyType.GetCustomAttribute<AllowNullAttribute>() != null
						|| p.PropertyType.GetCustomAttribute<MaybeNullAttribute>() != null
#else // .NET Standard 2.0
						|| !p.PropertyType.IsValueType
#endif
				}
			);
	}

	/// <summary>
	/// Tests if a particular table can be deserialized into the properties of a particular type.
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

			compatible &= propertyType != typeof(GristAttachment);
			if (throwOnIncompatible && !compatible)
				throw new InvalidOperationException(
					$"Use {nameof(GristAttachmentList)} to bind attachment columns instead of {nameof(GristAttachment)}"
				);

			// TODO: Type compatibility checking
		}

		return compatible;
	}

	/// <summary>
	/// Attempt to deserialize the fields of a Grist record into a given DTO.
	/// Automatically handles unwrapping, parsing, and conversion.
	/// </summary>
	/// <seealso cref="CanDeserialize"/>
	public static void Deserialize(IDictionary<string, object?> fields, T instance)
	{
		fields = fields.Where(i => BindingTargets.ContainsKey(i.Key)).ToDictionary();

		foreach ((string key, object? value) in fields)
		{
			BindingPoint binding = BindingTargets[key];

			// Early null value check for simple cell types
			// Handles empty text, choice, and Date(Time) cells
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

			object? convertedValue = GristValueConverter.ConvertValue(value, binding.PropertyType);
			switch (convertedValue)
			{
				// Opportunistic nulling:
				case ICollection { Count: 0 }
				or IGristReferenceList { Count: 0 }
				or GristAttachmentList { Count: 0 } when binding.IsNullable:
					convertedValue = null;
					break;

				case IGristReference:
				case IGristReferenceList rl when rl.Count != 0:
					// This only occurs when it's the generic type
					// TODO: Implement recursive binding for GristReference(List)<>
					throw new NotImplementedException();

				// Late null value check, handles empty reference cells
				case null when !binding.IsNullable:
					throw new InvalidOperationException(
						$"Got null/empty value in column {key}, but mapped property is not nullable."
					);
			}
			// TODO: Add options for handling un-convertable/null values (Assign null, throw, etc)
			binding.Property.SetValue(instance, convertedValue);
		}
	}
}
