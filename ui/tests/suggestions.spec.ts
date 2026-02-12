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

  await firstCard.getByRole('button', { name: 'Accept' }).click();
  await expect(list.locator('.suggestion-card')).toHaveCount(9);

  const nextCard = list.locator('.suggestion-card').first();
  await nextCard.locator('.header-row').click();
  await nextCard.getByRole('button', { name: 'Reject' }).click();
  await expect(list.locator('.suggestion-card')).toHaveCount(8);
});

test('project suggestion debug json copies to clipboard and shows bubble', async ({ page, context }) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await gotoScan(page);

  const firstCard = page.getByTestId('project-suggest-list').locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();

  await firstCard.getByTestId('debug-json-btn').click();
  await expect(page.getByText('Copied to clipboard')).toBeVisible();
});

test('suggestions scope shows pending from all scans and archive with accepted+rejected', async ({ page }) => {
  await gotoSuggestions(page);

  const list = page.getByTestId('project-suggest-list');
  await expect(list.locator('.suggestion-card')).toHaveCount(10);

  const firstCard = list.locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();
  await firstCard.getByRole('button', { name: 'Accept' }).click();

  const nextCard = list.locator('.suggestion-card').first();
  await nextCard.locator('.header-row').click();
  await nextCard.getByRole('button', { name: 'Reject' }).click();

  await expect(list.locator('.suggestion-card')).toHaveCount(8);

  await page.getByTestId('project-suggest-scope').getByText('Archive').click();
  await expect(list.locator('.suggestion-card')).toHaveCount(2);

  const statuses = await list.locator('.status').allTextContents();
  expect([...statuses].sort()).toEqual(['accepted', 'rejected']);
});

test('suggestions archive export shows exported popup and open folder action', async ({ page }) => {
  await gotoSuggestions(page);

  await page.getByTestId('project-suggest-export-archive').click();
  await expect(page.locator('.cdk-overlay-container .mat-mdc-tooltip-surface', { hasText: 'Exported' })).toBeVisible();

  await page.getByTestId('project-suggest-open-archive-folder').click();
  await expect(page.getByText(/Opened:/)).toBeVisible();
});
