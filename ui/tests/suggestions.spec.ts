import { test, expect } from '@playwright/test';
import type { Page } from '@playwright/test';

const gotoScan = async (page: Page) => {
  await page.goto('/scan');
  await expect(page.getByTestId('project-suggest-list')).toBeVisible();
  await expect(page.getByTestId('project-suggest-list').locator('[data-testid="project-name"]').first()).toBeVisible();
};

const gotoSuggestions = async (page: Page) => {
  await page.goto('/suggestions');
  await expect(page.getByTestId('project-suggest-list')).toBeVisible();
  await expect(page.getByTestId('project-suggest-scope')).toBeVisible();
};

const setMockSuggestions = async (page: Page, items: unknown[]) => {
  await page.addInitScript((mockItems) => {
    localStorage.setItem('mockSuggestions', JSON.stringify(mockItems));
  }, items);
};

const selectSort = async (page: Page, label: string) => {
  await page.getByTestId('project-suggest-sort').click();
  await page.getByRole('option', { name: label }).click();
};

const setSortDir = async (page: Page, dirLabel: 'Asc' | 'Desc') => {
  await page.getByTestId('project-suggest-sort-dir').getByText(dirLabel).click();
};

const getNames = async (page: Page) => {
  return page.getByTestId('project-suggest-list').locator('[data-testid="project-name"]').allTextContents();
};

test('project suggestions layout toggles between list and grid', async ({ page }) => {
  await gotoScan(page);

  const list = page.getByTestId('project-suggest-list');
  const toggle = page.getByTestId('project-suggest-layout');

  await toggle.getByText('Grid').click();
  await expect(list).toHaveClass(/layout-grid/);

  await toggle.getByText('List').click();
  await expect(list).not.toHaveClass(/layout-grid/);
});

test('project suggestions details toggle only in list view', async ({ page }) => {
  await gotoScan(page);

  const firstCard = page.getByTestId('project-suggest-list').locator('.suggestion-card').first();
  const firstDetails = firstCard.locator('.details');

  await expect(firstDetails).not.toBeVisible();

  await firstCard.locator('.header-row').click();
  await expect(firstDetails).toBeVisible();

  const toggle = page.getByTestId('project-suggest-layout');
  await toggle.getByText('Grid').click();

  await expect(firstDetails).toBeVisible();
});

test('project suggestions search filters by name', async ({ page }) => {
  await gotoScan(page);

  await page.getByTestId('project-suggest-search').fill('rust');
  await expect(page.getByTestId('project-suggest-list').locator('[data-testid="project-name"]')).toHaveCount(1);
  const names = await getNames(page);

  expect(names).toEqual(['rust-playground']);
});

test('project suggestions sort by name asc/desc', async ({ page }) => {
  await gotoScan(page);

  await selectSort(page, 'Name');
  await setSortDir(page, 'Asc');
  await expect(
    page.getByTestId('project-suggest-list').locator('[data-testid="project-name"]').first()
  ).toHaveText('c-labs');
  let names = await getNames(page);

  expect(names[0]).toBe('c-labs');
  expect(names[names.length - 1]).toBe('single-file-tool.ps1');

  await setSortDir(page, 'Desc');
  await expect(
    page.getByTestId('project-suggest-list').locator('[data-testid="project-name"]').first()
  ).toHaveText('single-file-tool.ps1');
  names = await getNames(page);

  expect(names[0]).toBe('single-file-tool.ps1');
});

test('project suggestions sort by score asc/desc', async ({ page }) => {
  await gotoScan(page);

  await selectSort(page, 'Score');
  await setSortDir(page, 'Asc');
  await expect(
    page.getByTestId('project-suggest-list').locator('[data-testid="project-name"]').first()
  ).toHaveText('course-js-2023');
  let names = await getNames(page);

  expect(names[0]).toBe('course-js-2023');

  await setSortDir(page, 'Desc');
  await expect(
    page.getByTestId('project-suggest-list').locator('[data-testid="project-name"]').first()
  ).toHaveText('dotnet-api');
  names = await getNames(page);
  expect(names[0]).toBe('dotnet-api');
});

test('project suggestions sort by created date asc/desc', async ({ page }) => {
  await gotoScan(page);

  await selectSort(page, 'Created');
  await setSortDir(page, 'Asc');
  await expect(
    page.getByTestId('project-suggest-list').locator('[data-testid="project-name"]').first()
  ).toHaveText('python-utils');
  let names = await getNames(page);

  expect(names[0]).toBe('python-utils');

  await setSortDir(page, 'Desc');
  await expect(
    page.getByTestId('project-suggest-list').locator('[data-testid="project-name"]').first()
  ).toHaveText('rust-playground');
  names = await getNames(page);
  expect(names[0]).toBe('rust-playground');
});

test('project suggestion accept/reject removes items from live pending list', async ({ page }) => {
  await gotoScan(page);

  const list = page.getByTestId('project-suggest-list');
  await expect(list.locator('.suggestion-card')).toHaveCount(10);

  const firstCard = page.getByTestId('project-suggest-list').locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();

  await firstCard.getByRole('button', { name: /^Accept$/ }).click();
  await expect(list.locator('.suggestion-card')).toHaveCount(9);

  const nextCard = list.locator('.suggestion-card').first();
  await nextCard.locator('.header-row').click();
  await nextCard.getByRole('button', { name: /^Reject$/ }).click();
  await expect(list.locator('.suggestion-card')).toHaveCount(8);
});

test('project suggestion reason copies to clipboard', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await gotoScan(page);

  const firstCard = page.getByTestId('project-suggest-list').locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();

  await firstCard.getByTestId('reason-copy-btn').click();
  await expect(page.getByText('Reason copied')).toBeVisible();
});

test('project suggestion path menu supports copy and open in explorer actions', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await gotoScan(page);

  const firstCard = page.getByTestId('project-suggest-list').locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();

  await firstCard.getByTestId('path-menu-trigger').click();
  await page.getByTestId('path-copy-btn').click();
  await expect(page.getByText('Path copied')).toBeVisible();

  await firstCard.getByTestId('path-menu-trigger').click();
  await page.getByTestId('path-open-btn').click();
  await expect(page.getByText('Opened in Explorer')).toBeVisible();
});

test('project suggestion path text stays right-aligned', async ({ page }) => {
  await gotoScan(page);

  const firstCard = page.getByTestId('project-suggest-list').locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();

  const trigger = firstCard.getByTestId('path-menu-trigger');
  const text = firstCard.locator('.path-value-text');

  const triggerBox = await trigger.boundingBox();
  const textBox = await text.boundingBox();
  if (!triggerBox || !textBox) {
    throw new Error('Missing path alignment bounds');
  }

  expect(Math.abs(triggerBox.x + triggerBox.width - (textBox.x + textBox.width))).toBeLessThanOrEqual(8);
});

test('suggestions scope shows pending from all scans and archive with accepted+rejected', async ({ page }) => {
  await gotoSuggestions(page);

  const list = page.getByTestId('project-suggest-list');
  await expect(list.locator('.suggestion-card')).toHaveCount(10);

  const firstCard = list.locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();
  await firstCard.getByRole('button', { name: /^Accept$/ }).click();

  const nextCard = list.locator('.suggestion-card').first();
  await nextCard.locator('.header-row').click();
  await nextCard.getByRole('button', { name: /^Reject$/ }).click();

  await expect(list.locator('.suggestion-card')).toHaveCount(8);

  await page.getByTestId('project-suggest-scope').getByText('Archive').click();
  await expect(list.locator('.suggestion-card')).toHaveCount(2);

  const statuses = await list.locator('.status').allTextContents();
  expect([...statuses].sort()).toEqual(['accepted', 'rejected']);
});

test('archive scope allows fixing rejected item and hides reject action', async ({ page }) => {
  await gotoSuggestions(page);

  const list = page.getByTestId('project-suggest-list');
  const firstCard = list.locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();
  await firstCard.getByRole('button', { name: /^Accept$/ }).click();

  const nextCard = list.locator('.suggestion-card').first();
  await nextCard.locator('.header-row').click();
  await nextCard.getByRole('button', { name: /^Reject$/ }).click();

  await page.getByTestId('project-suggest-scope').getByText('Archive').click();

  const archiveCards = list.locator('.suggestion-card');
  await expect(archiveCards).toHaveCount(2);

  const acceptedCard = archiveCards.filter({ has: page.locator('.status[data-status="accepted"]') }).first();
  await acceptedCard.locator('.header-row').click();
  await expect(acceptedCard.getByRole('button', { name: /^Reject$/ })).toHaveCount(0);

  const rejectedCard = archiveCards.filter({ has: page.locator('.status[data-status="rejected"]') }).first();
  await rejectedCard.locator('.header-row').click();
  await expect(rejectedCard.getByRole('button', { name: /^Accept$/ })).toHaveCount(1);
  await expect(rejectedCard.getByRole('button', { name: /^Reject$/ })).toHaveCount(0);
});

test('grid card size slider changes card dimensions', async ({ page }) => {
  await gotoScan(page);

  await page.getByTestId('project-suggest-layout').getByText('Grid').click();

  const firstCard = page.getByTestId('project-suggest-list').locator('.suggestion-card').first();
  const before = await firstCard.boundingBox();
  if (!before) {
    throw new Error('Missing initial card bounds');
  }

  await page.getByTestId('project-suggest-grid-size').fill('130');

  const after = await firstCard.boundingBox();
  if (!after) {
    throw new Error('Missing resized card bounds');
  }

  expect(after.height).toBeGreaterThan(before.height + 20);
});

test('pending scope deduplicates newest suggestion by path and kind', async ({ page }) => {
  const duplicateData = [
    {
      id: 'dup-1',
      scanSessionId: 'scan-old',
      rootPath: 'D:\\old',
      name: '2Dsource',
      score: 0.71,
      kind: 'ProjectRoot',
      path: 'D:\\old\\2Dsource',
      reason: 'markers: .sln',
      extensionsSummary: 'cpp=4',
      markers: ['.sln'],
      techHints: ['cpp'],
      createdAt: '2025-01-01T10:00:00.000Z',
      status: 'Pending'
    },
    {
      id: 'dup-2',
      scanSessionId: 'scan-new',
      rootPath: 'D:\\old',
      name: '2Dsource',
      score: 0.72,
      kind: 'ProjectRoot',
      path: 'D:\\old\\2Dsource',
      reason: 'markers: .vcxproj',
      extensionsSummary: 'cpp=4',
      markers: ['.vcxproj'],
      techHints: ['cpp'],
      createdAt: '2025-01-02T10:00:00.000Z',
      status: 'Pending'
    }
  ];

  await setMockSuggestions(page, duplicateData);
  await gotoScan(page);

  const names = page.getByTestId('project-suggest-list').locator('[data-testid="project-name"]');
  await expect(names).toHaveCount(1);
  await expect(names.first()).toHaveText('2Dsource');
});

test('suggestions archive export shows exported popup and open folder action', async ({ page }) => {
  await gotoSuggestions(page);

  await page.getByTestId('project-suggest-export-archive').click();
  await expect(page.locator('.cdk-overlay-container .mat-mdc-tooltip-surface', { hasText: 'Exported' })).toBeVisible();

  await page.getByTestId('project-suggest-open-archive-folder').click();
  await expect(page.getByText(/Opened:/)).toBeVisible();
});

test('suggestions page panels expand with viewport height', async ({ page }) => {
  await page.setViewportSize({ width: 1360, height: 720 });
  await gotoSuggestions(page);

  const projectPanel = page.locator('.card-grid .panel').first();
  await expect(projectPanel).toBeVisible();
  const before = await projectPanel.boundingBox();
  if (!before) {
    throw new Error('Missing project panel bounds');
  }

  await page.setViewportSize({ width: 1360, height: 900 });
  const after = await projectPanel.boundingBox();
  if (!after) {
    throw new Error('Missing resized project panel bounds');
  }

  expect(after.height).toBeGreaterThan(before.height + 80);
});
