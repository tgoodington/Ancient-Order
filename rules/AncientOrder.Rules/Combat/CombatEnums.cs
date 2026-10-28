namespace AncientOrder.Rules.Combat;

public enum ElementalPath
{
    Fire,
    Water,
    Air,
    Earth,
    Shadow,
    Light,
}

public enum ActionType
{
    Attack,
    Defend,
    Evade,
    Special,
    Group,
}

public enum CombatPhase
{
    AiDecision,
    VisualInfo,
    PcDeclaration,
    ActionResolution,
    PerAttack,
}

public enum DefenseType
{
    Block,
    Dodge,
    Parry,
    Defenseless,
}

/// <summary>Values equal the ascension level (0 to 3), so a cast to int gives the level.</summary>
public enum AscensionLevel
{
    Level0 = 0,
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
}

public enum CombatStatus
{
    Active,
    Victory,
    Defeat,
}

/// <summary>The numeric combatant stats a StatModifier (T4) can target.</summary>
public enum CombatStat
{
    Rank,
    MaxStamina,
    Power,
    Speed,
}

/// <summary>The stats a Buff can change; from the keys of the TypeScript BUFF_STAT_MAP (boost and debuff variants share a stat).</summary>
public enum BuffStat
{
    Power,
    Speed,
    BlockSR,
    BlockSMR,
    BlockFMR,
    DodgeSR,
    DodgeSMR,
    DodgeFMR,
    ParrySR,
    ParrySMR,
    ParryFMR,
}

/// <summary>Which energy table row applies to an event (the success/failure split is a separate flag).</summary>
public enum EnergyEvent
{
    Action,
    Reaction,
}
