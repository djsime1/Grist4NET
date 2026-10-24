using Microsoft.Kiota.Abstractions;

namespace Grist4NET.Kiota;

public class GristKiotaClient : GristKiotaClientBase
{
	public GristKiotaClient(IRequestAdapter requestAdapter)
		: base(requestAdapter) { }

	public new IRequestAdapter RequestAdapter => base.RequestAdapter;
}
