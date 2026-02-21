/**
 * Ancient Order - Express Application Entry Point
 *
 * Main Express application that mounts all API routers.
 */

import express, { Express, Request, Response, NextFunction } from 'express';
import { ApiResponse, ErrorCodes } from '../types';
import { createGameRouter } from './game';
import { createNpcRouter } from './npc';
import { createDialogueRouter } from './dialogue';
import { createPlayerRouter } from './player';

/**
 * Creates and configures the Express application.
 */
export function createApp(): Express {
  const app = express();

  // JSON body parser middleware
  app.use(express.json());

  // CORS headers middleware
  app.use((req: Request, res: Response, next: NextFunction) => {
    res.header('Access-Control-Allow-Origin', '*');
    res.header('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
    res.header('Access-Control-Allow-Headers', 'Origin, X-Requested-With, Content-Type, Accept, Authorization');

    // Handle preflight requests
    if (req.method === 'OPTIONS') {
      return res.sendStatus(200);
    }

    next();
  });

  // Mount routers
  app.use('/api/game', createGameRouter());
  app.use('/api/npc', createNpcRouter());
  app.use('/api/dialogue', createDialogueRouter());
  app.use('/api/player', createPlayerRouter());

  // 404 handler for unknown routes
  app.use((req: Request, res: Response) => {
    const response: ApiResponse = {
      success: false,
      error: {
        code: 'NOT_FOUND',
        message: `Route not found: ${req.method} ${req.path}`,
        details: {
          method: req.method,
          path: req.path,
        },
      },
    };
    return res.status(404).json(response);
  });

  // Global error handler
  app.use((err: Error, req: Request, res: Response, _next: NextFunction) => {
    console.error('Unhandled error:', err);

    const response: ApiResponse = {
      success: false,
      error: {
        code: ErrorCodes.VALIDATION_ERROR,
        message: err.message || 'An unexpected error occurred',
        details: {
          name: err.name,
          stack: process.env.NODE_ENV === 'development' ? err.stack : undefined,
        },
      },
    };

    return res.status(500).json(response);
  });

  return app;
}

/**
 * Starts the Express server on the configured port.
 */
export function startServer(): void {
  const port = process.env.PORT ? parseInt(process.env.PORT, 10) : 3000;
  const app = createApp();

  app.listen(port, () => {
    console.log(`Ancient Order API server running on port ${port}`);
  });
}

// Start server if this file is run directly
if (require.main === module) {
  startServer();
}
