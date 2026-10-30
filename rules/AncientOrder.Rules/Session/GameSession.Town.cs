using AncientOrder.Rules.Common;
using AncientOrder.Rules.Town;

namespace AncientOrder.Rules.Session;

public sealed partial class GameSession
{
    /// <summary>
    /// Moves the player to a point (meters, ground plane) of the current town. Refused with no town (TOWN_NOT_ACTIVE), during a
    /// conversation or a fight, for non-finite values, and off walkable ground (NOT_WALKABLE); the game is unchanged then.
    /// Walking from outside a present walk-over spot's area to inside it opens that spot's first entry that holds (the first such spot in
    /// content order wins). Report the position when the player stops, when it enters or leaves a walk-over area, and right before a trigger.
    /// </summary>
    public Result<TownEvent> MovePlayer(double x, double z) =>
        Run((c, s) => TownOperations.MovePlayer(c, s, x, z));

    /// <summary>
    /// Triggers a person present in the town: opens the first entry of what they open whose conditions hold, or does nothing (Nothing)
    /// when none holds. Refused with no town (TOWN_NOT_ACTIVE), during a conversation or a fight, when not present (TARGET_NOT_FOUND),
    /// and out of reach (OUT_OF_REACH); the game is unchanged then.
    /// </summary>
    public Result<TownEvent> TriggerPerson(string characterId) =>
        Run((c, s) => TownOperations.TriggerPerson(c, s, characterId));

    /// <summary>
    /// Triggers an interact spot present in the town, the same way as TriggerPerson. A walk-over spot is refused (VALIDATION_ERROR):
    /// those fire only when MovePlayer walks into them. Callers report the player's position with MovePlayer right before triggering.
    /// </summary>
    public Result<TownEvent> TriggerSpot(string spotId) =>
        Run((c, s) => TownOperations.TriggerSpot(c, s, spotId));
}
