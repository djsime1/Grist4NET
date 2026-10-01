namespace Grist4NET;

/// <summary>
/// When applied to a property, it will be ignored during property:column mapping.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class GristIgnoreAttribute : Attribute { }
