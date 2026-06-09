import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import EndpointMetadataEditor, { normalizeLabelRows, normalizeTagRows } from './EndpointMetadataEditor';

describe('EndpointMetadataEditor', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('normalizes blank metadata rows before API payloads are saved', () => {
    expect(normalizeLabelRows(['alpha', ' ', ' beta '])).toEqual(['alpha', 'beta']);
    expect(normalizeTagRows([
      { Key: ' team ', Value: ' ml ' },
      { Key: '', Value: 'ignored' },
      { Key: 'env', Value: '' }
    ])).toEqual({ team: 'ml', env: '' });
  });

  it('uses icon buttons to add and delete label and tag rows', async () => {
    const user = userEvent.setup();
    const onLabelsChange = vi.fn();
    const onTagsChange = vi.fn();

    render(
      <EndpointMetadataEditor
        labels={['alpha']}
        tags={{ owner: 'search' }}
        onLabelsChange={onLabelsChange}
        onTagsChange={onTagsChange}
      />
    );

    await user.click(screen.getByRole('button', { name: 'Add label' }));
    expect(onLabelsChange).toHaveBeenLastCalledWith(['alpha', '']);

    await user.click(screen.getByRole('button', { name: 'Delete label' }));
    expect(onLabelsChange).toHaveBeenLastCalledWith(['']);

    await user.click(screen.getByRole('button', { name: 'Add tag' }));
    expect(onTagsChange).toHaveBeenLastCalledWith([{ Key: 'owner', Value: 'search' }, { Key: '', Value: '' }]);

    await user.click(screen.getByRole('button', { name: 'Delete tag' }));
    expect(onTagsChange).toHaveBeenLastCalledWith([{ Key: '', Value: '' }]);
  });
});
