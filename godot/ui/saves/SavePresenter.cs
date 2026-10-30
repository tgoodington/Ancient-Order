using System.Collections.Immutable;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Saves;
using Godot;

namespace AncientOrder.Ui;

/// <summary>
/// What SavePresenter needs from the save and load screen (T20): the screen's root script implements this.
/// Render draws the ten slots in full; busy means an operation is running (disable rows and buttons, show a busy line); error is the inline refusal reason, or null.
/// The three events carry the slot number (1 to 10).
/// </summary>
public interface ISaveScreen
{
    void Render(ImmutableArray<SlotInfo> slots, SaveScreenMode mode, bool busy, string? error);

    event Action<int>? SaveRequested;

    event Action<int>? LoadRequested;

    event Action<int>? DeleteRequested;
}

/// <summary>
/// Lists, saves, loads, and deletes slots through the session, one operation at a time. The session calls are made on the main thread; every result is handed back
/// to the main thread explicitly (CallDeferred) before anything else is touched. A refusal shows inline on the save screen. A successful load goes to ScreenFlow.NotifyLoaded.
/// Inert when the screen does not implement ISaveScreen (the flow's placeholder).
/// </summary>
public sealed class SavePresenter
{
    private readonly ScreenFlow _flow;
    private readonly ISaveScreen? _screen;
    private ImmutableArray<SlotInfo> _slots = ImmutableArray<SlotInfo>.Empty;
    private SaveScreenMode _mode = SaveScreenMode.LoadOnly;
    private bool _busy;
    private string? _error;
    private LoadReport? _loaded;

    public SavePresenter(ScreenFlow flow, ISaveScreen? screen)
    {
        _flow = flow;
        _screen = screen;
        if (screen is null)
        {
            return;
        }

        flow.SavesOpened += OnOpened;
        screen.SaveRequested += slot => Operate(slot, Operation.Save);
        screen.LoadRequested += slot => Operate(slot, Operation.Load);
        screen.DeleteRequested += slot => Operate(slot, Operation.Delete);
    }

    private enum Operation
    {
        Save,
        Load,
        Delete,
    }

    public bool IsBusy => _busy;

    /// <summary>
    /// Lists the slots for any screen (the start screen's summary). The callback always runs on the main thread, never after the flow is freed, and a refusal arrives as a failed Result.
    /// Read only: it does not touch the busy state or the save screen.
    /// </summary>
    public async void RequestSlots(Action<Result<ImmutableArray<SlotInfo>>> onListed)
    {
        Result<ImmutableArray<SlotInfo>> result;
        try
        {
            if (_flow.Session is null)
            {
                result = Result<ImmutableArray<SlotInfo>>.Fail(new RuleError(ErrorCode.INTERNAL_ERROR, "No game session"));
            }
            else
            {
                result = await _flow.Session.ListSlots();
                await ToMainThread();
            }
        }
        catch (Exception ex)
        {
            await ToMainThread();
            result = Result<ImmutableArray<SlotInfo>>.Fail(new RuleError(ErrorCode.INTERNAL_ERROR, ex.Message));
        }

        if (!GodotObject.IsInstanceValid(_flow))
        {
            return;
        }

        try
        {
            onListed(result);
        }
        catch (Exception ex)
        {
            GD.PrintErr("RequestSlots callback failed: " + ex.Message);
        }
    }

    private bool Alive => GodotObject.IsInstanceValid(_flow) && (_screen is not GodotObject node || GodotObject.IsInstanceValid(node));

    private void Draw()
    {
        if (Alive)
        {
            _screen!.Render(_slots, _mode, _busy, _error);
        }
    }

    private void OnOpened(SaveScreenMode mode)
    {
        _mode = mode;
        _error = null;
        if (!_busy)
        {
            Run(() => Refresh());
        }
        else
        {
            Draw();
        }
    }

    private async void Run(Func<Task> work)
    {
        _busy = true;
        Draw();
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            await ToMainThread();
            _error = ex.Message;
        }

        _busy = false;
        if (!Alive)
        {
            return;
        }

        if (_loaded is { } report)
        {
            _loaded = null;
            try
            {
                _flow.NotifyLoaded(report);
            }
            catch (Exception ex)
            {
                GD.PrintErr("Load notification failed: " + ex.Message);
            }

            return;
        }

        Draw();
    }

    private void Operate(int slot, Operation operation)
    {
        if (_busy || _flow.Session is null)
        {
            return;
        }

        if (operation == Operation.Save && _mode == SaveScreenMode.LoadOnly)
        {
            _error = "Saving is not available here.";
            Draw();
            return;
        }

        _error = null;
        Run(() => Execute(slot, operation));
    }

    private async Task Execute(int slot, Operation operation)
    {
        var session = _flow.Session!;
        RuleError? refusal = null;
        LoadReport? report = null;
        switch (operation)
        {
            case Operation.Save:
                {
                    var result = await session.SaveToSlot(slot);
                    await ToMainThread();
                    refusal = result.IsOk ? null : result.Error;
                    break;
                }

            case Operation.Delete:
                {
                    var result = await session.DeleteSlot(slot);
                    await ToMainThread();
                    refusal = result.IsOk ? null : result.Error;
                    break;
                }

            default:
                {
                    var result = await session.LoadFromSlot(slot);
                    await ToMainThread();
                    if (result.IsOk)
                    {
                        report = result.Value;
                    }
                    else
                    {
                        refusal = result.Error;
                    }

                    break;
                }
        }

        if (report is not null)
        {
            _loaded = report;
            return;
        }

        if (!Alive)
        {
            return;
        }

        _error = refusal?.Message;
        await Refresh();
    }

    private async Task Refresh()
    {
        var result = await _flow.Session!.ListSlots();
        await ToMainThread();
        if (!Alive)
        {
            return;
        }
        if (result.IsOk)
        {
            _slots = result.Value;
        }
        else
        {
            _error ??= result.Error.Message;
        }
    }

    private static Task ToMainThread()
    {
        if (OS.GetThreadCallerId() == OS.GetMainThreadId())
        {
            return Task.CompletedTask;
        }

        var done = new TaskCompletionSource();
        Callable.From(() => done.SetResult()).CallDeferred();
        return done.Task;
    }
}
