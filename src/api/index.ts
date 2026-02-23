/**
 * Ancient Order - Fastify Application Entry Point
 *
 * App factory that creates and configures the Fastify instance.
 * Registers all route plugins and attaches session state via decorate().
 *
 * Session state sharing: Fastify encapsulates each plugin in its own scope.
 * Assigning `fastify.gameState = value` inside a plugin only sets the value
 * on that plugin's local scope — other plugins still read the root's null.
 * To share mutable state across all plugins, we decorate a container OBJECT.
 * Mutating `fastify.gameStateContainer.state = value` is visible everywhere
 * because all plugins hold a reference to the SAME container object.
 *
 * Error handler order: setErrorHandler must be called BEFORE register() so
 * that encapsulated plugins inherit the handler. Registering plugins first
 * means they capture the default Fastify error handler instead.
 */

import Fastify, { FastifyInstance, FastifyError, FastifyRequest, FastifyReply } from 'fastify';
import { GameState, ApiResponse, ErrorCodes } from '../types/index.js';
import { gamePlugin } from './game.js';
import { playerPlugin } from './player.js';
import { npcPlugin } from './npc.js';
import { dialoguePlugin } from './dialogue.js';

// ============================================================================
// Shared session state container
// ============================================================================

/**
 * Mutable container holding the active game state.
 * Decorated onto the Fastify instance as a plain object so that all plugins
 * share the SAME reference — mutating .state is visible everywhere.
 */
export interface GameStateContainer {
  state: GameState | null;
}

// Type-safe declaration merging so all plugins can access fastify.gameStateContainer.
declare module 'fastify' {
  interface FastifyInstance {
    gameStateContainer: GameStateContainer;
  }
}

// ============================================================================
// Global Error Handler
// ============================================================================

/**
 * Catches all unhandled errors thrown by route handlers.
 * Returns a structured ApiResponse with an error envelope instead of a
 * raw stack trace or Fastify's default error shape.
 *
 * IMPORTANT: This handler must be registered (via setErrorHandler) BEFORE
 * any plugins are registered with fastify.register(). Otherwise, encapsulated
 * plugins capture the default Fastify error handler at registration time and
 * do not inherit the custom handler.
 */
async function globalErrorHandler(
  error: FastifyError,
  _request: FastifyRequest,
  reply: FastifyReply
): Promise<void> {
  // Fastify validation errors (JSON Schema failures) carry statusCode 400.
  if (error.statusCode === 400 || error.validation) {
    const response: ApiResponse<never> = {
      success: false,
      error: {
        code: ErrorCodes.VALIDATION_ERROR,
        message: error.message,
      },
    };
    await reply.code(400).send(response);
    return;
  }

  // Domain errors that carry an error code on the Error object
  const domainCode =
    (error as NodeJS.ErrnoException & { code?: string }).code ?? null;

  if (domainCode === ErrorCodes.SAVE_NOT_FOUND) {
    const response: ApiResponse<never> = {
      success: false,
      error: { code: ErrorCodes.SAVE_NOT_FOUND, message: error.message },
    };
    await reply.code(404).send(response);
    return;
  }

  if (domainCode === ErrorCodes.INVALID_SLOT) {
    const response: ApiResponse<never> = {
      success: false,
      error: { code: ErrorCodes.INVALID_SLOT, message: error.message },
    };
    await reply.code(400).send(response);
    return;
  }

  // Dialogue-specific errors thrown by processDialogueSelection
  if (error.message.includes('not available')) {
    const response: ApiResponse<never> = {
      success: false,
      error: { code: ErrorCodes.DIALOGUE_OPTION_NOT_AVAILABLE, message: error.message },
    };
    await reply.code(400).send(response);
    return;
  }

  if (error.message.includes('node not found') || error.message.includes('Node not found')) {
    const response: ApiResponse<never> = {
      success: false,
      error: { code: ErrorCodes.DIALOGUE_NODE_NOT_FOUND, message: error.message },
    };
    await reply.code(404).send(response);
    return;
  }

  if (error.message.includes('option not found') || error.message.includes('Option not found')) {
    const response: ApiResponse<never> = {
      success: false,
      error: { code: ErrorCodes.DIALOGUE_OPTION_NOT_FOUND, message: error.message },
    };
    await reply.code(404).send(response);
    return;
  }

  // Fallback: generic 500
  const response: ApiResponse<never> = {
    success: false,
    error: {
      code: 'INTERNAL_ERROR',
      message: 'An unexpected error occurred',
    },
  };
  await reply.code(500).send(response);
}

// ============================================================================
// App Factory
// ============================================================================

/**
 * Creates and configures the Fastify application.
 * Returns the configured Fastify instance (not yet listening).
 */
export async function buildApp(): Promise<FastifyInstance> {
  const fastify = Fastify({ logger: true });

  // Attach a mutable container for the active game state.
  // All plugins share the SAME container object — mutating .state is visible
  // across all plugin scopes. This avoids Fastify's plugin encapsulation issue
  // where assigning fastify.gameState inside one plugin does not propagate to
  // sibling plugins.
  const container: GameStateContainer = { state: null };
  fastify.decorate('gameStateContainer', container);

  // IMPORTANT: Register the global error handler BEFORE any plugins.
  // Plugins capture the active error handler at registration time.
  fastify.setErrorHandler(globalErrorHandler);

  // Health check endpoint
  fastify.get('/health', async () => {
    return { status: 'ok' };
  });

  // Register route plugins with their URL prefixes.
  await fastify.register(gamePlugin, { prefix: '/api/game' });
  await fastify.register(playerPlugin, { prefix: '/api/player' });
  await fastify.register(npcPlugin, { prefix: '/api/npc' });
  await fastify.register(dialoguePlugin, { prefix: '/api/dialogue' });

  return fastify;
}

// ============================================================================
// Server Entry Point
// ============================================================================

/**
 * Starts the Fastify server on the configured port.
 */
async function startServer(): Promise<void> {
  const port = process.env.PORT ? parseInt(process.env.PORT, 10) : 3000;
  const fastify = await buildApp();

  try {
    await fastify.listen({ port, host: '0.0.0.0' });
  } catch (err) {
    fastify.log.error(err);
    process.exit(1);
  }
}

// Start server only when this file is the direct entry point.
// Importing this module in tests will NOT trigger the server to start.
import { fileURLToPath } from 'url';
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  startServer();
}
