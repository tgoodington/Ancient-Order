using AncientOrder.Rules.Combat;
using AncientOrder.Rules.Combat.Ai;
using AncientOrder.Rules.Tests.Fixtures;

namespace AncientOrder.Rules.Tests.Combat.Ai;

// Carried over from src/combat/behaviorTree/perception.test.ts (buildPerception becomes Perception.Build). toBeCloseTo(x) with no precision
// is 2 decimals, kept as precision 2. makeCombatant(overrides) becomes TestCombatants.Make(id) with a record `with`; a KO fighter is stamina 0 plus IsKO.
public class BuildPerception
{
    private static Combatant C(string id) => TestCombatants.Make(id);

    private static Combatant Pct(string id, double stamina, double maxStamina = 100) => TestCombatants.Make(id) with { Stamina = stamina, MaxStamina = maxStamina };

    private static CombatState State(Combatant[] players, Combatant[] enemies, int round = 1) => TestCombatants.State(players, enemies, round);

    public class SelfFields
    {
        [Fact(DisplayName = "computes selfStaminaPct correctly")]
        public void Self_stamina_pct()
        {
            var self = Pct("enemy_1", 50);
            var p = Perception.Build(self, State([C("player_1")], [self]));
            Assert.Equal(0.5, p.SelfStaminaPct, 2);
        }

        [Fact(DisplayName = "sets selfId, selfEnergy, selfRank, selfPath from combatant")]
        public void Self_fields()
        {
            var self = C("enemy_x") with { Rank = 4.5, Energy = 3, ElementalPath = ElementalPath.Shadow, Stamina = 80, MaxStamina = 80 };
            var p = Perception.Build(self, State([C("player_1")], [self]));
            Assert.Equal("enemy_x", p.SelfId);
            Assert.Equal(4.5, p.SelfRank);
            Assert.Equal(3, p.SelfEnergy);
            Assert.Equal(ElementalPath.Shadow, p.SelfPath);
        }

        [Fact(DisplayName = "handles maxStamina=0 without dividing by zero (selfStaminaPct=0)")]
        public void Zero_max_stamina()
        {
            var self = Pct("e1", 0, 0);
            var p = Perception.Build(self, State([C("p1")], [self]));
            Assert.Equal(0, p.SelfStaminaPct);
        }
    }

    public class AllyPerceptionGroup
    {
        [Fact(DisplayName = "excludes self from allies list")]
        public void Excludes_self()
        {
            var self = C("enemy_1");
            var ally = C("enemy_2");
            var p = Perception.Build(self, State([C("player_1")], [self, ally]));
            Assert.True(p.Allies.All(a => a.Id != "enemy_1"));
            Assert.Contains(p.Allies, a => a.Id == "enemy_2");
        }

        [Fact(DisplayName = "sorts allies by staminaPct ascending (weakest first)")]
        public void Sorts_weakest_first()
        {
            var self = C("e1");
            var ally1 = Pct("e2", 80);
            var ally2 = Pct("e3", 30);
            var p = Perception.Build(self, State([C("p1")], [self, ally1, ally2]));
            Assert.Equal("e3", p.Allies[0].Id);
            Assert.Equal("e2", p.Allies[1].Id);
        }

        [Fact(DisplayName = "computes lowestAllyStaminaPct correctly")]
        public void Lowest_ally_pct()
        {
            var self = C("e1");
            var ally1 = Pct("e2", 90);
            var ally2 = Pct("e3", 20);
            var p = Perception.Build(self, State([C("p1")], [self, ally1, ally2]));
            Assert.Equal(0.2, p.LowestAllyStaminaPct, 2);
        }

        [Fact(DisplayName = "lowestAllyStaminaPct is 1.0 when no allies exist")]
        public void No_allies()
        {
            var self = C("e1");
            var p = Perception.Build(self, State([C("p1")], [self]));
            Assert.Equal(1.0, p.LowestAllyStaminaPct);
            Assert.Equal(0, p.AllyCount);
        }

        [Fact(DisplayName = "excludes KO allies from allyCount and lowestAllyStaminaPct")]
        public void Excludes_ko_allies()
        {
            var self = C("e1");
            var allyKO = TestCombatants.Ko(Pct("e2", 0));
            var allyAlive = Pct("e3", 60);
            var p = Perception.Build(self, State([C("p1")], [self, allyKO, allyAlive]));
            Assert.Equal(1, p.AllyCount);
            Assert.Equal(0.6, p.LowestAllyStaminaPct, 2);
        }
    }

    public class TeamAverageStamina
    {
        [Fact(DisplayName = "includes self in teamAvgStaminaPct")]
        public void Includes_self()
        {
            var self = Pct("e1", 50);
            var ally = Pct("e2", 100);
            var p = Perception.Build(self, State([C("p1")], [self, ally]));
            Assert.Equal(0.75, p.TeamAvgStaminaPct, 2);
        }
    }

    public class EnemyPerceptionGroup
    {
        [Fact(DisplayName = "sorts enemies by staminaPct ascending")]
        public void Sorts_ascending()
        {
            var self = C("e1");
            var enemy1 = Pct("p1", 90);
            var enemy2 = Pct("p2", 40);
            var p = Perception.Build(self, State([enemy1, enemy2], [self]));
            Assert.Equal("p2", p.Enemies[0].Id);
            Assert.Equal("p1", p.Enemies[1].Id);
        }

        [Fact(DisplayName = "computes speedDelta correctly")]
        public void Speed_delta()
        {
            var self = C("e1") with { Speed = 15 };
            var enemy = C("p1") with { Speed = 10 };
            var p = Perception.Build(self, State([enemy], [self]));
            Assert.Equal(0.5, p.Enemies[0].SpeedDelta, 2);
        }

        [Fact(DisplayName = "computes rankDelta correctly")]
        public void Rank_delta()
        {
            var self = C("e1") with { Rank = 5.0 };
            var enemy = C("p1") with { Rank = 3.0 };
            var p = Perception.Build(self, State([enemy], [self]));
            Assert.Equal(2.0, p.Enemies[0].RankDelta, 2);
        }

        [Fact(DisplayName = "computes weakestEnemyStaminaPct")]
        public void Weakest_enemy_pct()
        {
            var self = C("e1");
            var enemy1 = Pct("p1", 80);
            var enemy2 = Pct("p2", 25);
            var p = Perception.Build(self, State([enemy1, enemy2], [self]));
            Assert.Equal(0.25, p.WeakestEnemyStaminaPct, 2);
        }

        [Fact(DisplayName = "weakestEnemyStaminaPct is 1.0 when all enemies are KO")]
        public void All_enemies_ko()
        {
            var self = C("e1");
            var enemy = TestCombatants.Ko(C("p1"));
            var p = Perception.Build(self, State([enemy], [self]));
            Assert.Equal(1.0, p.WeakestEnemyStaminaPct);
            Assert.Equal(0, p.EnemyCount);
        }

        [Fact(DisplayName = "handles negative speedDelta when self is slower")]
        public void Negative_speed_delta()
        {
            var self = C("e1") with { Speed = 5 };
            var enemy = C("p1") with { Speed = 15 };
            var p = Perception.Build(self, State([enemy], [self]));
            Assert.Equal(-0.667, p.Enemies[0].SpeedDelta, 2);
        }
    }

    public class PartyIdentification
    {
        [Fact(DisplayName = "identifies enemy party combatant correctly — enemies become foes")]
        public void Enemy_party()
        {
            var self = C("enemy_npc");
            var player = C("player_hero");
            var p = Perception.Build(self, State([player], [self]));
            Assert.Contains(p.Enemies, e => e.Id == "player_hero");
            Assert.DoesNotContain(p.Allies, a => a.Id == "player_hero");
        }

        [Fact(DisplayName = "identifies player party combatant correctly — enemies become foes")]
        public void Player_party()
        {
            var self = C("player_hero");
            var enemy = C("enemy_npc");
            var p = Perception.Build(self, State([self], [enemy]));
            Assert.Contains(p.Enemies, e => e.Id == "enemy_npc");
        }

        [Fact(DisplayName = "includes round from state")]
        public void Round_from_state()
        {
            var self = C("e1");
            var p = Perception.Build(self, State([C("p1")], [self], 4));
            Assert.Equal(4, p.Round);
        }
    }
}
