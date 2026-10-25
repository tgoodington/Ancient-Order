import { StrictMode } from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render } from '@testing-library/react';
import { SceneStage } from './SceneStage';
import type { CreateRenderer, StageRenderer } from '../../render/renderer';

function makeFake() {
  const live = new Set<StageRenderer>();
  const created: StageRenderer[] = [];
  const createRenderer = vi.fn<CreateRenderer>(() => {
    const renderer: StageRenderer = {
      showScene: vi.fn(),
      destroy: vi.fn(() => {
        live.delete(renderer);
      }),
    };
    live.add(renderer);
    created.push(renderer);
    return renderer;
  });
  return { live, created, createRenderer };
}

describe('SceneStage', () => {
  it('creates a renderer with the initial scene and a background color', () => {
    const { createRenderer } = makeFake();
    render(<SceneStage scene="stage" createRenderer={createRenderer} />);

    expect(createRenderer).toHaveBeenCalledTimes(1);
    const [parent, options] = createRenderer.mock.calls[0];
    expect(parent).toBeInstanceOf(HTMLElement);
    expect(options.initialScene).toBe('stage');
    expect(options.backgroundColor).toBeTruthy();
  });

  it('leaves exactly one live renderer under StrictMode', () => {
    const { live, createRenderer } = makeFake();
    render(
      <StrictMode>
        <SceneStage scene="stage" createRenderer={createRenderer} />
      </StrictMode>,
    );

    expect(live.size).toBe(1);
  });

  it('destroys the renderer on unmount', () => {
    const { live, created, createRenderer } = makeFake();
    const { unmount } = render(
      <SceneStage scene="stage" createRenderer={createRenderer} />,
    );
    unmount();

    expect(live.size).toBe(0);
    expect(created[0].destroy).toHaveBeenCalledTimes(1);
  });

  it('asks the renderer to show the scene prop', () => {
    const { created, createRenderer } = makeFake();
    render(<SceneStage scene="stage" createRenderer={createRenderer} />);

    expect(created[0].showScene).toHaveBeenCalledWith('stage');
  });
});
