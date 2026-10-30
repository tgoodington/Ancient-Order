using System.Diagnostics.CodeAnalysis;

namespace AncientOrder.Rules.Town;

/// <summary>What a move or trigger did. ConversationStarted names the conversation that opened; HandedOff names the fight the player left for.</summary>
public abstract record TownEvent
{
    private protected TownEvent()
    {
    }

    [SuppressMessage("Naming", "CA1716", Justification = "The task names this case Nothing; it is always used as TownEvent.Nothing.")]
    public sealed record Nothing : TownEvent;

    public sealed record ConversationStarted(string ConversationId) : TownEvent;

    public sealed record HandedOff(string FightId) : TownEvent;
}
