import { describe, it, expect } from 'vitest';
import { duplicateName } from './duplicate';
import { withoutMaskedSecrets } from './providers';

describe('duplicateName', () => {
  it('appends and increments a copy suffix', () => {
    expect(duplicateName('Prod Embeddings')).toBe('Prod Embeddings (copy)');
    expect(duplicateName('Prod Embeddings (copy)')).toBe('Prod Embeddings (copy 2)');
    expect(duplicateName('Prod Embeddings (copy 2)')).toBe('Prod Embeddings (copy 3)');
    expect(duplicateName('  ')).toBe('');
    expect(duplicateName(null)).toBe('');
  });
});

describe('withoutMaskedSecrets', () => {
  it('clears masked secrets and keeps everything else', () => {
    const stored = {
      Id: 'cep_1',
      ApiKey: '****abcd',
      Model: 'm',
      ProviderSettings: { Bedrock: { Region: 'us-east-1', AccessKeyId: 'AKIA', SecretAccessKey: '****WXYZ', SessionToken: null } },
    };
    const copy = withoutMaskedSecrets(stored);
    expect(copy.ApiKey).toBeNull();
    expect(copy.ProviderSettings.Bedrock).toEqual({ Region: 'us-east-1', AccessKeyId: 'AKIA', SecretAccessKey: null, SessionToken: null });
    expect(stored.ProviderSettings.Bedrock.SecretAccessKey).toBe('****WXYZ');
    expect(withoutMaskedSecrets({ ApiKey: 'plain' }).ApiKey).toBe('plain');
  });
});
