namespace AncientOrder.Rules.Combat;

/// <summary>SR = success rate, SMR = success mitigation rate, FMR = failure mitigation rate; all 0 to 1.</summary>
public sealed record DefenseSkill(double SR, double SMR, double FMR);

public sealed record ReactionSkills(DefenseSkill Block, DefenseSkill Dodge, DefenseSkill Parry);

/// <summary>Accumulated reaction XP per reaction (ADR-054 Layer B); capped at 1675.</summary>
public sealed record ReactionProgress(int Block, int Dodge, int Parry);
