using System.Runtime.CompilerServices;
using Microsoft.Kiota.Abstractions;

namespace Grist4NET;

/// <summary>
/// Wrapper for exceptions thrown by API requests, contains request info and body for easier diagnosis.
/// </summary>
public sealed class GristApiException : Exception
{
	internal GristApiException(
		string message,
		ApiException innerException,
		RequestInformation request,
		[CallerMemberName] string source = "undefined"
	)
		: base(
			$"Exception in {source} operation: {message}\n\t({request.HttpMethod} {request.URI})",
			innerException
		)
	{
		RequestInformation = request;
		if (request.Content is { Length: > 0 })
		{
			using StreamReader sr = new(request.Content);
			RequestBody = sr.ReadToEnd();
		}
	}

	/// <summary>
	/// Parameters of the request.
	/// </summary>
	public RequestInformation RequestInformation { get; }

	/// <summary>
	/// Stringified request body, if present.
	/// </summary>
	public string? RequestBody { get; }
}
