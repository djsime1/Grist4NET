using Microsoft.Kiota.Abstractions;

namespace Grist4NET.Kiota;

public class GristKiotaClient(IRequestAdapter requestAdapter)
	: GristKiotaClientBase(requestAdapter);
