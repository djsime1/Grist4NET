namespace Grist4NET;

public sealed class GristAttachmentList
{
	internal GristAttachmentList() { }

	public GristDocument Document { get; internal init; }
	public string DocumentId => Document.DocumentId;
	public IReadOnlyList<ulong> AttachmentIds { get; internal init; }
	public int Count => AttachmentIds.Count;

	public async Task<GristAttachment> Resolve(int index) =>
		await Document.GetAttachment(AttachmentIds[index]);

	public async IAsyncEnumerable<GristAttachment> ResolveAll()
	{
		for (int i = 0; i < Count; i++)
			yield return await Resolve(i);
	}
}
