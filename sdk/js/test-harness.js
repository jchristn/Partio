/**
 * Partio JavaScript SDK Test Harness.
 */

import { PartioClient, PartioError } from './partio-sdk.js';

const endpoint = process.argv[2] || 'http://localhost:8000';
const adminKey = process.argv[3] || 'partioadmin';

console.log('Partio JavaScript SDK Test Harness');
console.log(`Endpoint: ${endpoint}`);
console.log(`Admin Key: ${adminKey}`);
console.log();

let passed = 0;
let failed = 0;
const failedTests = [];
const totalStart = Date.now();

const client = new PartioClient(endpoint, adminKey);

let testTenantId = null;
let testUserId = null;
let testCredId = null;
let testEpId = null;

async function runTest(name, fn) {
  const start = Date.now();
  try {
    await fn();
    const elapsed = Date.now() - start;
    console.log(`  PASS  ${name} (${elapsed}ms)`);
    passed++;
  } catch (ex) {
    const elapsed = Date.now() - start;
    console.log(`  FAIL  ${name} (${elapsed}ms) - ${ex.message}`);
    failed++;
    failedTests.push(name);
  }
}

// Health
await runTest('Health Check', async () => {
  const result = await client.health();
  if (!result || result.Status !== 'Healthy') throw new Error('Not healthy');
});

await runTest('Who Am I', async () => {
  const result = await client.whoami();
  if (!result || !result.Role) throw new Error('No role');
});

// Tenant CRUD
await runTest('Create Tenant', async () => {
  const tenant = await client.createTenant({ Name: 'Test Tenant', Labels: ['test'] });
  if (!tenant || !tenant.Id) throw new Error('No response');
  testTenantId = tenant.Id;
});

await runTest('Read Tenant', async () => {
  const tenant = await client.getTenant(testTenantId);
  if (!tenant || tenant.Name !== 'Test Tenant') throw new Error('Mismatch');
});

await runTest('Update Tenant', async () => {
  const updated = await client.updateTenant(testTenantId, { Name: 'Updated Tenant' });
  if (!updated || updated.Name !== 'Updated Tenant') throw new Error('Update failed');
});

await runTest('Tenant Exists (HEAD)', async () => {
  if (!await client.tenantExists(testTenantId)) throw new Error('Should exist');
});

await runTest('Enumerate Tenants', async () => {
  const result = await client.enumerateTenants();
  if (!result || !result.Data || result.Data.length === 0) throw new Error('No tenants');
});

// User CRUD
await runTest('Create User', async () => {
  const user = await client.createUser({ TenantId: testTenantId, Email: 'test@test.com', Password: 'testpass' });
  if (!user || !user.Id) throw new Error('No response');
  testUserId = user.Id;
});

await runTest('Read User', async () => {
  const user = await client.getUser(testUserId);
  if (!user || user.Email !== 'test@test.com') throw new Error('Mismatch');
});

await runTest('Enumerate Users', async () => {
  const result = await client.enumerateUsers();
  if (!result || !result.Data || result.Data.length === 0) throw new Error('No users');
});

// Credential CRUD
await runTest('Create Credential', async () => {
  const cred = await client.createCredential({ TenantId: testTenantId, UserId: testUserId, Name: 'Test Key' });
  if (!cred || !cred.Id) throw new Error('No response');
  testCredId = cred.Id;
});

await runTest('Read Credential', async () => {
  const cred = await client.getCredential(testCredId);
  if (!cred || cred.Name !== 'Test Key') throw new Error('Mismatch');
});

await runTest('Enumerate Credentials', async () => {
  const result = await client.enumerateCredentials();
  if (!result || !result.Data || result.Data.length === 0) throw new Error('No credentials');
});

// Endpoint CRUD
await runTest('Create Endpoint', async () => {
  const ep = await client.createEndpoint({ TenantId: testTenantId, Model: 'test-model', Endpoint: 'http://localhost:11434', ApiFormat: 'Ollama' });
  if (!ep || !ep.Id) throw new Error('No response');
  testEpId = ep.Id;
});

await runTest('Read Endpoint', async () => {
  const ep = await client.getEndpoint(testEpId);
  if (!ep || ep.Model !== 'test-model') throw new Error('Mismatch');
});

await runTest('Enumerate Endpoints', async () => {
  const result = await client.enumerateEndpoints();
  if (!result || !result.Data || result.Data.length === 0) throw new Error('No endpoints');
});

// Request History
await runTest('Enumerate Request History', async () => {
  const result = await client.enumerateRequestHistory();
  if (!result) throw new Error('No response');
});

// Process Single Cell (requires an active embedding endpoint)
await runTest('Process Single Cell', async () => {
  const eps = await client.enumerateEndpoints();
  const activeEp = eps && eps.Data ? eps.Data.find(e => e.Active !== false) : null;
  if (!activeEp) throw new Error('SKIP: no active embedding endpoint');

  const result = await client.process(activeEp.Id, {
    Type: 'Text',
    Text: 'Partio is a multi-tenant embedding platform.',
    ChunkingConfiguration: { Strategy: 'FixedTokenCount', FixedTokenCount: 256 },
    EmbeddingConfiguration: { L2Normalization: false },
    Labels: ['test'],
    Tags: { source: 'sdk-test' }
  });

  if (!result) throw new Error('No response');
  if (!result.Text) throw new Error('Missing Text');
  if (!result.Chunks || result.Chunks.length === 0) throw new Error('No chunks');
  if (!result.Chunks[0].Embeddings || result.Chunks[0].Embeddings.length === 0) throw new Error('No embeddings');
  if (!result.Chunks[0].Labels || result.Chunks[0].Labels.length === 0) throw new Error('No labels on chunk');
  if (!result.Chunks[0].Tags || Object.keys(result.Chunks[0].Tags).length === 0) throw new Error('No tags on chunk');
});

// Error cases
await runTest('Unauthenticated Request (401)', async () => {
  const badClient = new PartioClient(endpoint, 'invalid-token');
  try {
    await badClient.enumerateTenants();
    throw new Error('Expected 401');
  } catch (ex) {
    if (!(ex instanceof PartioError) || ex.statusCode !== 401) throw ex;
  }
});

await runTest('Non-existent Resource (404)', async () => {
  try {
    await client.getTenant('nonexistent-id-12345');
    throw new Error('Expected 404');
  } catch (ex) {
    if (!(ex instanceof PartioError) || ex.statusCode !== 404) throw ex;
  }
});

// Cleanup
await runTest('Delete Endpoint', async () => {
  await client.deleteEndpoint(testEpId);
  if (await client.endpointExists(testEpId)) throw new Error('Still exists');
});

await runTest('Delete Credential', async () => {
  await client.deleteCredential(testCredId);
});

await runTest('Delete User', async () => {
  await client.deleteUser(testUserId);
});

await runTest('Delete Tenant', async () => {
  await client.deleteTenant(testTenantId);
  if (await client.tenantExists(testTenantId)) throw new Error('Still exists');
});

const totalMs = Date.now() - totalStart;

console.log();
console.log('=== SUMMARY ===');
console.log(`Total: ${passed + failed}  Passed: ${passed}  Failed: ${failed}`);
console.log(`Runtime: ${totalMs}ms`);
console.log(`Result: ${failed === 0 ? 'PASS' : 'FAIL'}`);

if (failedTests.length > 0) {
  console.log();
  console.log('Failed tests:');
  for (const name of failedTests) {
    console.log(`  - ${name}`);
  }
}

console.log('================');
process.exit(failed === 0 ? 0 : 1);
