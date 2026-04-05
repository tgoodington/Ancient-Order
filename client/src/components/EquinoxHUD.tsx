import { useGameState } from '../context/GameStateContext';
import type { Combatant } from '../types/index';
import { ErrorBanner } from './ErrorBanner';
import { LoadingScreen } from './LoadingScreen';
import { StaminaBar } from './StaminaBar';
import { EnergyDisplay } from './EnergyDisplay';
import { PersonalityBreakdown } from './PersonalityBreakdown';
import { TeamSelector } from './TeamSelector';
import styles from './EquinoxHUD.module.css';

export function EquinoxHUD() {
  const { gameState, npcs, loading, error, setTeam, dismissError } = useGameState();

  const isInCombat = gameState?.combatState !== null && gameState?.combatState !== undefined;
  const isInNarrative = gameState?.narrativeState != null;

  let playerCombatant: Combatant | null = null;
  if (gameState?.combatState) {
    playerCombatant =
      gameState.combatState.playerParty.find(c => c.id.startsWith('player_')) ?? null;
  }

  return (
    <div className={styles.hud}>
      <div className={styles.frame}>
        <h2 className={styles.title}>{gameState?.player.name ?? 'Ancient Order'}</h2>

        {error && <ErrorBanner message={error} onDismiss={dismissError} />}

        {loading && <LoadingScreen />}

        {!loading && gameState && (
          <>
            {/* Combat section -- conditional (D1) */}
            {playerCombatant && (
              <section className={styles.combatSection} aria-label="Combat stats">
                <StaminaBar
                  current={playerCombatant.stamina}
                  max={playerCombatant.maxStamina}
                  label={gameState.player.name}
                />
                <EnergyDisplay
                  current={playerCombatant.energy}
                  max={playerCombatant.maxEnergy}
                  label={gameState.player.name}
                />
              </section>
            )}

            {/* Personality -- always visible */}
            <section className={styles.personalitySection} aria-label="Personality">
              <PersonalityBreakdown personality={gameState.player.personality} />
            </section>

            {/* Team selector -- always visible, disabled during combat/narrative */}
            <section className={styles.teamSection} aria-label="Team selection">
              <TeamSelector
                currentTeam={gameState.team}
                npcs={npcs}
                onSetTeam={setTeam}
                disabled={isInCombat || isInNarrative}
              />
            </section>
          </>
        )}
      </div>
    </div>
  );
}
