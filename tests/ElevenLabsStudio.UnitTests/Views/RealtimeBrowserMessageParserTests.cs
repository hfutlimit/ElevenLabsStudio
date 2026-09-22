using ElevenLabsStudio.Core.Domain;
using ElevenLabsStudio.Services;
using FluentAssertions;

namespace ElevenLabsStudio.UnitTests.Views;

public sealed class RealtimeBrowserMessageParserTests
{
	[Fact]
	public void Parses_connected_message_with_conversation_id()
	{
		var message = RealtimeBrowserMessageParser.Parse(
			"""{"type":"connected","conversationId":"conv_123"}""");

		message.Type.Should().Be("connected");
		message.Status.Should().Be(RealtimeConversationStatus.Connected);
		message.ConversationId.Should().Be("conv_123");
	}

	[Fact]
	public void Parses_transcript_message_without_accepting_raw_payloads()
	{
		var message = RealtimeBrowserMessageParser.Parse(
			"""{"type":"message","speaker":"agent","text":"Welcome","at":"2026-09-22T00:00:00Z"}""");

		message.Transcript.Should().NotBeNull();
		message.Transcript!.Speaker.Should().Be("agent");
		message.Transcript.Text.Should().Be("Welcome");
		message.Transcript.At.Should().Be(DateTimeOffset.Parse("2026-09-22T00:00:00Z"));
	}
}
