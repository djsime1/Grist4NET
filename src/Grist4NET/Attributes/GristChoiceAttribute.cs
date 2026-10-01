namespace Grist4NET;

/// <summary>
/// When applied to an enum member, a choice with this name in Grist will be mapped to the enum.
/// </summary>
/// <param name="choice">Choice name in Grist</param>
[AttributeUsage(AttributeTargets.Field)]
public sealed class GristChoiceAttribute(string choice) : Attribute
{
	/// <summary>
	/// Choice name in Grist
	/// </summary>
	public string Choice { get; } = choice;
}
