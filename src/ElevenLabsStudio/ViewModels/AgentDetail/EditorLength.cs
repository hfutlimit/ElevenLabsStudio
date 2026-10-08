namespace ElevenLabsStudio.ViewModels.AgentDetail;

/// <summary>
/// Character-count label shared by the prompt and first-message editors.
/// The warn threshold belongs to each tab; this only picks the tone.
/// </summary>
internal static class EditorLength
{
	public static string Text(int length) => length.ToString("N0") + " chars";

	public static string BrushKey(int length, int warnAt)
	{
		var near = (int)(warnAt * 0.9);
		return length switch
		{
			0 => "App.TextMuted",
			_ when length > warnAt => "App.Danger",
			_ when length > near => "App.Accent",
			_ => "App.TextSecondary",
		};
	}
}
