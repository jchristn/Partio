import { expect, test } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const partioServerUrl = 'http://localhost:8400';
const screenshotDir = path.join(process.cwd(), 'test-results', 'model-load-responsive');

const embeddingEndpoint = {
  Id: 'embedding-responsive-1',
  TenantId: 'default',
  Name: 'Responsive embedding endpoint',
  Model: 'nomic-embed-text-with-an-excessively-long-model-alias-for-responsive-validation',
  Endpoint: 'http://localhost:11434/ollama/embedding/with/a/very/long/path/for/layout-validation',
  ApiFormat: 'Ollama',
  Active: true,
  EnableRequestHistory: true,
  MaximumTimeoutMs: 60000,
  MaxConcurrentRequests: 2,
  HealthCheckEnabled: true,
  Tokenization: null
};

const completionEndpoint = {
  Id: 'completion-responsive-1',
  TenantId: 'default',
  Name: 'Responsive inference endpoint',
  Model: 'gemma3:4b-with-an-excessively-long-model-alias-for-responsive-validation',
  Endpoint: 'http://localhost:11434/ollama/completion/with/a/very/long/path/for/layout-validation',
  ApiFormat: 'Ollama',
  Active: true,
  EnableRequestHistory: true,
  MaximumTimeoutMs: 60000,
  MaxConcurrentRequests: 2,
  HealthCheckEnabled: true
};

const pages = [
  {
    name: 'embedding',
    path: '/endpoints/embeddings',
    heading: 'Embedding Endpoints',
    rowText: embeddingEndpoint.Model,
    loadPath: `/v1.0/endpoints/embedding/${embeddingEndpoint.Id}/load`
  },
  {
    name: 'completion',
    path: '/endpoints/inference',
    heading: 'Inference Endpoints',
    rowText: completionEndpoint.Model,
    loadPath: `/v1.0/endpoints/completion/${completionEndpoint.Id}/load`
  }
];

const viewports = [
  { name: 'desktop', width: 1280, height: 800 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'mobile', width: 390, height: 844 }
];

async function mockPartioApi(page) {
  await page.addInitScript((url) => {
    localStorage.setItem('partio_serverUrl', url);
    localStorage.setItem('partio_bearerToken', 'partioadmin');
    localStorage.setItem('partio_hasCompletedTour', 'true');
    localStorage.setItem('partio_hasCompletedSetup', 'true');
  }, partioServerUrl);

  await page.route('**/v1.0/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const pathname = url.pathname;

    if (pathname === '/v1.0/health') {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ Success: true }) });
      return;
    }

    if (pathname === '/v1.0/whoami') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ Role: 'Admin', TenantName: 'Default' })
      });
      return;
    }

    if (pathname === '/v1.0/tenants/enumerate') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ Data: [{ Id: 'default', Name: 'Default' }], TotalRecords: 1 })
      });
      return;
    }

    if (pathname === '/v1.0/endpoints/embedding/enumerate') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ Data: [embeddingEndpoint], TotalRecords: 1 })
      });
      return;
    }

    if (pathname === '/v1.0/endpoints/completion/enumerate') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ Data: [completionEndpoint], TotalRecords: 1 })
      });
      return;
    }

    if (pathname === '/v1.0/endpoints/embedding/health'
      || pathname === '/v1.0/endpoints/completion/health') {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) });
      return;
    }

    if (pathname.endsWith('/load')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          Success: true,
          StatusCode: 200,
          Outcome: 'Loaded',
          Strategy: 'NativeProviderLoad',
          Message: 'Model loaded for responsive validation.',
          ResponseTimeMs: 37.5,
          RequestHistoryId: 'request-history-responsive-1',
          EmbeddingCalls: pathname.includes('/embedding/') ? [{}] : null,
          CompletionCalls: pathname.includes('/completion/') ? [{}] : null
        })
      });
      return;
    }

    await route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ Message: pathname }) });
  });
}

async function assertNoHorizontalOverflow(page) {
  const scrollX = await page.evaluate(() => {
    window.scrollTo(9999, 0);
    return window.scrollX;
  });
  expect(scrollX).toBe(0);

  const modalHasOverflow = await page.locator('.modal-content').evaluate((element) => element.scrollWidth > element.clientWidth + 1);
  expect(modalHasOverflow).toBe(false);
}

test.describe('model load responsive smoke', () => {
  test.beforeEach(async ({ page }) => {
    fs.mkdirSync(screenshotDir, { recursive: true });
    await mockPartioApi(page);
  });

  for (const scenario of pages) {
    for (const viewport of viewports) {
      test(`${scenario.name} load modal at ${viewport.name}`, async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await page.goto(scenario.path);

        await expect(page.getByRole('heading', { name: scenario.heading })).toBeVisible();
        const row = page.locator('tbody tr', { hasText: scenario.rowText }).first();
        await expect(row).toBeVisible();

        await row.locator('.action-menu-trigger').click();
        await page.getByRole('button', { name: 'Load Model' }).click();
        await expect(page.getByRole('heading', { name: 'Load Model' })).toBeVisible();
        await expect(page.getByLabel('Strategy')).toBeVisible();
        await expect(page.getByRole('button', { name: 'Load' })).toBeVisible();

        await assertNoHorizontalOverflow(page);
        await page.screenshot({
          path: path.join(screenshotDir, `${scenario.name}-${viewport.name}-modal.png`),
          fullPage: true
        });

        await page.getByRole('button', { name: 'Load' }).click();
        await expect(page.getByText('Model loaded for responsive validation.')).toBeVisible();
        await expect(page.getByText('Success')).toBeVisible();
        await assertNoHorizontalOverflow(page);
        await expect(page.getByRole('button', { name: 'Close' })).toBeVisible();

        expect(scenario.loadPath).toContain('/load');
        await page.screenshot({
          path: path.join(screenshotDir, `${scenario.name}-${viewport.name}-result.png`),
          fullPage: true
        });
      });
    }
  }
});
