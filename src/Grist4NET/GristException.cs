namespace Grist4NET;

public sealed class GristException : Exception
{
	public GristException()
	{
		// TODO: wrap API calls with this to provide hints as to what specific request failed
	}
}
