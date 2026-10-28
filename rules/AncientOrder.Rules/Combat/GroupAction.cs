using AncientOrder.Rules.Common;

namespace AncientOrder.Rules.Combat;

public static class GroupAction
{
    public static GroupActionConfig DefaultConfig { get; } = new(1.5, GroupEnergyRequirement.Full);

    /// <summary>
    /// Resolves a GROUP: every living member of the leader's party strikes, the summed damage is multiplied, the target is forced
    /// to Block (against its buffed rates), and every participant's energy is reset to 0. Draws one roll (the Block roll) when it
    /// fires. A KO'd target still drains the participants' energy but deals nothing.
    /// </summary>
    public static ActionResolution Resolve(CombatState state, GroupActionDeclaration declaration, GroupActionConfig config, IRollSource rolls)
    {
        var leaderId = declaration.LeaderId;
        var targetId = declaration.TargetId;

        var leader = CombatStateEdits.Find(state, leaderId);
        if (leader is null || leader.IsKO)
        {
            return new ActionResolution(state, new ActionResult(leaderId, ActionType.Group, null));
        }

        var target = CombatStateEdits.Find(state, targetId);
        if (target is null || target.IsKO)
        {
            return new ActionResolution(DrainParticipantEnergy(state, leaderId), new ActionResult(leaderId, ActionType.Group, null));
        }

        var participants = CombatStateEdits.OwnParty(state, leaderId).Where(c => !c.IsKO).ToList();

        var total = 0.0;
        foreach (var participant in participants)
        {
            total += Formulas.CalculateBaseDamage(participant.Power);
        }

        var groupDamage = total * config.DamageMultiplier;

        var block = Defense.EffectiveReactionSkills(target).Block;
        var blockRoll = rolls.Roll();
        var blockOutcome = Defense.ResolveBlock(groupDamage, block.SR, block.SMR, block.FMR, blockRoll);
        var damageMultiplier = blockOutcome.Success ? 1 - block.SMR : 1 - block.FMR;

        var finalDamage = blockOutcome.Damage;
        var current = CombatStateEdits.ApplyStaminaDelta(state, targetId, -finalDamage);

        foreach (var participant in participants)
        {
            var fresh = CombatStateEdits.Find(current, participant.Id);
            if (fresh is not null)
            {
                current = CombatStateEdits.Replace(current, fresh with { Energy = 0 });
            }
        }

        var attackResult = new AttackResult(
            leaderId,
            targetId,
            finalDamage,
            DefenseType.Block,
            new DefenseResult(DefenseType.Block, blockOutcome.Success, damageMultiplier),
            false,
            false,
            false,
            false);

        return new ActionResolution(current, new ActionResult(leaderId, ActionType.Group, attackResult));
    }

    private static CombatState DrainParticipantEnergy(CombatState state, string leaderId)
    {
        var current = state;
        foreach (var participant in CombatStateEdits.OwnParty(state, leaderId).Where(c => !c.IsKO))
        {
            var fresh = CombatStateEdits.Find(current, participant.Id);
            if (fresh is not null)
            {
                current = CombatStateEdits.Replace(current, fresh with { Energy = 0 });
            }
        }

        return current;
    }
}
