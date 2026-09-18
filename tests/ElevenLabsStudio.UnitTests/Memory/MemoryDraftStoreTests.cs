using ElevenLabsStudio.Infrastructure.Memory;
using FluentAssertions;
using Xunit;

namespace ElevenLabsStudio.UnitTests.Memory;

public sealed class MemoryDraftStoreTests
{
    [Fact]
    public void Save_then_TryGet_round_trips()
    {
        var store = new MemoryDraftStore();
        var draft = new TestDraft("hello", 42);

        store.Save("agent:abc", draft);
        store.TryGet<TestDraft>("agent:abc", out var read).Should().BeTrue();
        read.Should().Be(draft);
    }

    [Fact]
    public void TryGet_unknown_key_returns_false()
    {
        var store = new MemoryDraftStore();
        store.TryGet<TestDraft>("missing", out _).Should().BeFalse();
    }

    [Fact]
    public void Has_tracks_presence()
    {
        var store = new MemoryDraftStore();
        store.Has("nope").Should().BeFalse();
        store.Save("k", new TestDraft("x", 1));
        store.Has("k").Should().BeTrue();
    }

    [Fact]
    public void Discard_removes_key()
    {
        var store = new MemoryDraftStore();
        store.Save("k", new TestDraft("x", 1));
        store.Discard("k");
        store.Has("k").Should().BeFalse();
    }

    [Fact]
    public void Save_overwrites_previous_value_at_same_key()
    {
        var store = new MemoryDraftStore();
        store.Save("k", new TestDraft("first", 1));
        store.Save("k", new TestDraft("second", 2));
        store.TryGet<TestDraft>("k", out var read).Should().BeTrue();
        read!.Name.Should().Be("second");
        read.Count.Should().Be(2);
    }

    [Fact]
    public void ClearAll_removes_every_key()
    {
        var store = new MemoryDraftStore();
        store.Save("a", new TestDraft("a", 1));
        store.Save("b", new TestDraft("b", 1));
        store.Keys.Should().HaveCount(2);

        store.ClearAll();
        store.Keys.Should().BeEmpty();
    }

    [Fact]
    public void Keys_enumerate_every_active_key()
    {
        var store = new MemoryDraftStore();
        store.Save("a", new TestDraft("a", 1));
        store.Save("b", new TestDraft("b", 1));
        store.Save("c", new TestDraft("c", 1));
        store.Keys.Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    private sealed record TestDraft(string Name, int Count);
}