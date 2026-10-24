using Grist4NET.Kiota;
using Grist4NET.Kiota.Models;
using Microsoft.Kiota.Abstractions;

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
		AttachmentMetadata metadata;
		try
		{
			metadata = (await Api.Docs[DocumentId].Attachments[AttachmentRef].GetAsync())!;
		}
		catch (ApiException e)
		{
			RequestInformation req = Api.Docs[DocumentId]
				.Attachments[AttachmentRef]
				.ToGetRequestInformation();
			throw new GristApiException("Attachment metadata", e, req);
		}

		FileName = metadata.FileName!;
		FileSize = Convert.ToUInt64(metadata.FileSize);
		TimeUploaded = DateTime.Parse(metadata.TimeUploaded!);
	}

	public async Task<Stream> Fetch()
	{
		Stream file;
		try
		{
			file = (await Api.Docs[DocumentId].Attachments[AttachmentRef].Download.GetAsync())!;
		}
		catch (ApiException e)
		{
			RequestInformation req = Api.Docs[DocumentId]
				.Attachments[AttachmentRef]
				.Download.ToGetRequestInformation();
			throw new GristApiException("Attachment download", e, req);
		}

		return file;
	}

	public async Task Fetch(Stream copyTo)
	{
		using Stream file = await Fetch();
		await file.CopyToAsync(copyTo);
	}

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
