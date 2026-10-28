using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Session;
using AncientOrder.Rules.State;
using Godot;

namespace AncientOrder;

/// <summary>
/// The composition root: the one place that builds the content, the save store on the player's machine, and the session.
/// Future screens get the session from here.
/// </summary>
public sealed class GameHost
{
    public GameHost()
    {
        Content = StandInContent.Create();
        var backend = new FileSaveBackend(ProjectSettings.GlobalizePath("user://saves"));
        Session = new GameSession(Content, new GameSessionOptions
        {
            Saves = new SaveStore(backend, Content),
            OnListenerError = error => GD.PushError(error.ToString()),
        });
    }

    public GameContent Content { get; }

    public GameSession Session { get; }
}
