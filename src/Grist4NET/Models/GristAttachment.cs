using Grist4NET.Kiota;
using Grist4NET.Kiota.Models;

namespace Grist4NET;

public sealed class GristAttachment
{
	internal readonly GristKiotaClient Api;
	public GristDocument Document { get; }
	public string DocumentId => Document.DocumentId;
	public ulong AttachmentRef { get; }
	public string FileName { get; internal set; }
	public ulong FileSize { get; internal set; }
	public DateTime TimeUploaded { get; internal set; }

	internal GristAttachment(ulong attachmentRef, GristDocument document, GristKiotaClient api)
	{
		AttachmentRef = attachmentRef;
		Document = document;
		Api = api;
	}

	internal async Task<GristAttachment> Initialize(
		Dictionary<ulong, GristAttachment>? cache = null
	)
	{
		await Refresh();
		cache?.Add(AttachmentRef, this);
		return this;
	}

	// Attachments are immutable, i think
	internal async Task Refresh()
	{
		AttachmentMetadata metadata = (
			await Api.Docs[DocumentId].Attachments[AttachmentRef].GetAsync()
		)!;
		FileName = metadata.FileName!;
		FileSize = Convert.ToUInt64(metadata.FileSize);
		TimeUploaded = DateTime.Parse(metadata.TimeUploaded!);
	}

	public Task<Stream> Fetch() =>
		Api.Docs[DocumentId].Attachments[AttachmentRef].Download.GetAsync()!;

#if _
	private Stream? _cachedFile;

	public async Task<Stream> Fetch() => _cachedFile ?? await FetchInternal();

	private async Task<Stream> FetchInternal()
	{
		_cachedFile = await Api.Docs[DocumentId].Attachments[AttachmentRef].Download.GetAsync();
		return _cachedFile;
	}
#endif
}
