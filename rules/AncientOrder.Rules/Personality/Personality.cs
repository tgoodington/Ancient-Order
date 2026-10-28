namespace AncientOrder.Rules.Personality;

public sealed record Personality
{
    public double Patience { get; init; }
    public double Empathy { get; init; }
    public double Cunning { get; init; }
    public double Logic { get; init; }
    public double Kindness { get; init; }
    public double Charisma { get; init; }

    public double this[Trait trait] => trait switch
    {
        Trait.Patience => Patience,
        Trait.Empathy => Empathy,
        Trait.Cunning => Cunning,
        Trait.Logic => Logic,
        Trait.Kindness => Kindness,
        Trait.Charisma => Charisma,
        _ => throw new ArgumentOutOfRangeException(nameof(trait)),
    };

    public Personality With(Trait trait, double value) => trait switch
    {
        Trait.Patience => this with { Patience = value },
        Trait.Empathy => this with { Empathy = value },
        Trait.Cunning => this with { Cunning = value },
        Trait.Logic => this with { Logic = value },
        Trait.Kindness => this with { Kindness = value },
        Trait.Charisma => this with { Charisma = value },
        _ => throw new ArgumentOutOfRangeException(nameof(trait)),
    };
}
