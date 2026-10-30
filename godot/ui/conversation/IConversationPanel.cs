using AncientOrder.Rules.Conversations;

namespace AncientOrder.Ui;

/// <summary>
/// What the conversation presenter needs from the panel scene's root script (T17). ScreenFlow owns the panel's visibility; the panel only fills and clears.
/// The events are plain C# events (not Godot signals). Content text is shown as plain text, never markup.
/// </summary>
public interface IConversationPanel
{
    /// <summary>The player took the choice with this id (a locked choice is never raised).</summary>
    event Action<string> ChoiceTaken;

    /// <summary>The player took Continue on a step with no choices.</summary>
    event Action ContinueTaken;

    /// <summary>The player pressed Close on a final outcome (see ShowOutcome).</summary>
    event Action CloseTaken;

    /// <summary>
    /// Shows the current step (replaces any step shown before; focus goes to the first available choice). If ShowOutcome(outcome, false) was called since the last ShowStep or Close,
    /// that outcome is shown above the step, once; a ShowStep with no staged outcome shows none.
    /// </summary>
    void ShowStep(ConversationView view);

    /// <summary>
    /// final false: stages the outcome of the choice just taken, to be shown with the next ShowStep (draw nothing yet).
    /// final true: the conversation ended; shows only the outcome (no step, no choices) with a single Close button, focused, which raises CloseTaken. The panel does not hide itself.
    /// </summary>
    void ShowOutcome(ConversationOutcome outcome, bool final);

    /// <summary>Clears the transcript, choices and any staged outcome. Called by the presenter when the conversation is over; the flow hides the panel.</summary>
    void Close();
}
