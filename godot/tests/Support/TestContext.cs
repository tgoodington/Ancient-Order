using AncientOrder.Rules.Content.Check;
using AncientOrder.Rules.Content.Packs;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using AncientOrder.Ui;
using Godot;

namespace AncientOrder.Tests;

/// <summary>A game started through StartGame: the flow on screen, its session (null when the pack failed its check), the content check report, and the save backend it runs over.</summary>
public sealed record StartedGame(ScreenFlow Flow, GameSession? Session, ContentCheckReport Report, ISaveBackend Saves);

/// <summary>A save backend whose every call fails, for testing a slot list that cannot be read.</summary>
public sealed class FailingSaveBackend : ISaveBackend
{
    public ValueTask<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default) => throw new SaveStorageException("Save storage is unavailable");

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => throw new SaveStorageException("Save storage is unavailable");

    public ValueTask WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken = default) => throw new SaveStorageException("Save storage is unavailable");

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) => throw new SaveStorageException("Save storage is unavailable");
}

/// <summary>
/// What one test gets: a Root node in the tree that the runner frees afterwards (add everything under it), frame waits, input, and a way to start the game
/// from an in-memory pack with in-memory saves. Waits go by frames, never by the clock. Games and saves are in memory only: nothing touches user://.
/// </summary>
public sealed class TestContext
{
    private const int MaxWaitFrames = 600;

    private readonly SceneTree _tree;

    public TestContext(SceneTree tree, Node root)
    {
        _tree = tree;
        Root = root;
    }

    public Node Root { get; }

    public async Task NextFrame() => await Root.ToSignal(_tree, SceneTree.SignalName.ProcessFrame);

    public async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await NextFrame();
        }
    }

    /// <summary>Waits frame by frame until the condition holds; fails the test (naming what it waited for) after 600 frames.</summary>
    public async Task WaitUntil(Func<bool> condition, string what)
    {
        for (var i = 0; i < MaxWaitFrames; i++)
        {
            if (condition())
            {
                return;
            }

            await NextFrame();
        }

        if (!condition())
        {
            Assert.Fail($"Gave up waiting for {what}");
        }
    }

    /// <summary>Sends an action press and release through the input system, then waits a frame.</summary>
    public async Task Press(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
        await NextFrame();
    }

    /// <summary>Presses a button as a click would (fails if it is hidden or disabled), then waits a frame.</summary>
    public async Task Click(BaseButton button)
    {
        Assert.True(button.IsVisibleInTree(), $"Cannot click {button.Name}: it is not visible");
        Assert.False(button.Disabled, $"Cannot click {button.Name}: it is disabled");
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await NextFrame();
    }

    /// <summary>Runs the pack through the content check, as the game does at launch.</summary>
    public static ContentCheckReport Check(IReadOnlyDictionary<string, string> pack) => ContentCheck.Run(new MemoryContentSource(pack));

    /// <summary>A session over the content with the given backend (a new MemorySaveBackend when none), on a fixed clock.</summary>
    public static GameSession NewSession(GameContent content, ISaveBackend? saves = null) =>
        new(content, new GameSessionOptions
        {
            TimeProvider = new FixedTimeProvider(),
            Saves = new SaveStore(saves ?? new MemorySaveBackend(), content),
        });

    /// <summary>
    /// Checks the pack, builds a session over the backend, puts main.tscn under Root with AutoStart off, and starts the flow (the content error
    /// screen when the pack fails its check). Waits a frame so the first screen has rendered. Pass the same backend to a second call to start
    /// again over the same saves.
    /// </summary>
    public async Task<StartedGame> StartGame(IReadOnlyDictionary<string, string> pack, ISaveBackend? saves = null)
    {
        saves ??= new MemorySaveBackend();
        var report = Check(pack);
        var session = report.Content is null ? null : NewSession(report.Content, saves);
        var flow = GD.Load<PackedScene>("res://main.tscn").Instantiate<ScreenFlow>();
        flow.AutoStart = false;
        Root.AddChild(flow);
        flow.Start(report.Content, session, report);
        await NextFrame();
        return new StartedGame(flow, session, report, saves);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
    }
}
