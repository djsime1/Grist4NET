using Grist4NET.Kiota;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace Grist4NET;

/// <summary>
/// Represents an API session to a single Grist instance. Start here.
/// </summary>
public class GristConnection
{
	internal static readonly Uri DefaultBaseUrl = new("https://docs.getgrist.com/api");

	/// <summary>
	/// Raw Kiota client for API methods not covered by wrapper types.
	/// </summary>
	public GristKiotaClient Api { get; }
	internal readonly string ApiKey;
	internal readonly Uri BaseUrl;

	// Document ID's are immutable, good enough to be cache PK
	private readonly Dictionary<string, GristDocument> _documentCache = new();
	private readonly SemaphoreSlim _getDocumentSemaphore = new(1, 1);

	/// <param name="apiKey"><see href="https://support.getgrist.com/rest-api/#api-keys"/></param>
	/// <param name="baseUrl">Defaults to Grist's hosted SAAS</param>
	public GristConnection(string apiKey, Uri? baseUrl = null)
	{
		ArgumentException.ThrowIfNullOrEmpty(apiKey);

		ApiKey = apiKey;
		BaseUrl = baseUrl ?? DefaultBaseUrl;

		ApiKeyAuthenticationProvider authProvider = new(
			$"Bearer {ApiKey}",
			"Authorization",
			ApiKeyAuthenticationProvider.KeyLocation.Header,
			BaseUrl.Host
		);

		HttpClientRequestAdapter requestAdapter = new(authProvider)
		{
			BaseUrl = BaseUrl.ToString(),
		};
		Api = new GristKiotaClient(requestAdapter);
	}

	/// <seealso cref="GristConnectionOptions"/>
	public GristConnection(GristConnectionOptions options)
		: this(options.ApiKey, options.BaseUrl) { }

	/// <summary>
	/// Used by dependency injection and options pattern.
	/// </summary>
	/// <example><code>
	/// var builder = Host.CreateApplicationBuilder(args);
	/// builder.Services.Configure&lt;GristConnectionOptions&gt;(builder.Configuration.GetSection("Grist"));
	/// builder.Services.AddSingleton&lt;GristConnection&gt;();
	/// var app = builder.Build();
	/// var grist = app.Services.GetRequiredService&lt;GristConnection&gt;();
	/// </code></example>
	/// <seealso href="https://learn.microsoft.com/en-us/dotnet/core/extensions/options"/>
	public GristConnection(IOptions<GristConnectionOptions> options)
		: this(options.Value) { }

	/// <summary>
	/// Fetches a document by its full ID, returns a wrapper object that can be used to access tables.
	/// </summary>
	public async Task<GristDocument> GetDocument(string documentId)
	{
		await _getDocumentSemaphore.WaitAsync();
		try
		{
			return _documentCache.TryGetValue(documentId, out GristDocument? document)
				? document
				: await new GristDocument(documentId, Api).Initialize(_documentCache);
		}
		finally
		{
			_getDocumentSemaphore.Release();
		}
	}
}

/// <summary>
/// Stores the required options to create a <see cref="GristConnection"/>.
/// </summary>
public class GristConnectionOptions
{
	/// <seealso href="https://support.getgrist.com/rest-api/#api-keys"/>
	public required string ApiKey { get; init; }

	/// <summary>Defaults to <c>https://docs.getgrist.com/api</c> if not specified</summary>.
	public Uri? BaseUrl { get; init; }
}
