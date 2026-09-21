namespace ElevenLabsStudio.Core.Exceptions;

public sealed class WorkflowUpdateException : Exception
{
	public WorkflowUpdateException(string message)
		: base(message)
	{
	}

	public WorkflowUpdateException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
