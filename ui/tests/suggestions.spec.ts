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

const handleProjectAcceptDialog = async (
  page: Page,
  action: 'skip' | 'heuristics' | 'ai' = 'skip'
) => {
  const dialog = page.locator('mat-dialog-container').last();
  await expect(dialog.getByRole('heading', { name: 'Project accepted' })).toBeVisible();

  const buttonMap: Record<typeof action, string> = {
    skip: 'project-accept-action-skip-btn',
    heuristics: 'project-accept-action-heuristics-btn',
    ai: 'project-accept-action-ai-btn'
  };

  await dialog.getByTestId(buttonMap[action]).click();
  await expect(dialog).toBeHidden();
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
  await handleProjectAcceptDialog(page, 'skip');
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

test('suggestions scope splits accepted and rejected views', async ({ page }) => {
  await gotoSuggestions(page);

  const list = page.getByTestId('project-suggest-list');
  await expect(list.locator('.suggestion-card')).toHaveCount(10);

  const firstCard = list.locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();
  await firstCard.getByRole('button', { name: /^Accept$/ }).click();
  await handleProjectAcceptDialog(page, 'skip');

  const nextCard = list.locator('.suggestion-card').first();
  await nextCard.locator('.header-row').click();
  await nextCard.getByRole('button', { name: /^Reject$/ }).click();

  await expect(list.locator('.suggestion-card')).toHaveCount(8);

  await page.getByTestId('project-suggest-scope').getByText('Accepted').click();
  await expect(list.locator('.suggestion-card')).toHaveCount(1);
  await expect(list.locator('.status').first()).toHaveText('accepted');

  await page.getByTestId('project-suggest-scope').getByText('Rejected').click();
  await expect(list.locator('.suggestion-card')).toHaveCount(1);
  await expect(list.locator('.status').first()).toHaveText('rejected');
});

test('accepted scope hides mutating actions', async ({ page }) => {
  await gotoSuggestions(page);

  const list = page.getByTestId('project-suggest-list');
  const firstCard = list.locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();
  await firstCard.getByRole('button', { name: /^Accept$/ }).click();
  await handleProjectAcceptDialog(page, 'skip');

  const nextCard = list.locator('.suggestion-card').first();
  await nextCard.locator('.header-row').click();
  await nextCard.getByRole('button', { name: /^Reject$/ }).click();

  await page.getByTestId('project-suggest-scope').getByText('Accepted').click();
  const acceptedCard = list.locator('.suggestion-card').first();
  await acceptedCard.locator('.header-row').click();
  await expect(acceptedCard.getByRole('button', { name: /^Accept$/ })).toHaveCount(0);
  await expect(acceptedCard.getByRole('button', { name: /^Reject$/ })).toHaveCount(0);
  await expect(acceptedCard.getByTestId('project-suggest-delete-btn')).toHaveCount(0);
});

test('rejected scope allows deleting rejected suggestion', async ({ page }) => {
  await gotoSuggestions(page);

  const list = page.getByTestId('project-suggest-list');
  const firstCard = list.locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();
  await firstCard.getByRole('button', { name: /^Reject$/ }).click();

  await page.getByTestId('project-suggest-scope').getByText('Rejected').click();
  await expect(list.locator('.suggestion-card')).toHaveCount(1);
  await page.getByTestId('project-suggest-layout').getByText('Grid').click();

  const rejectedCard = list.locator('.suggestion-card').first();
  await expect(rejectedCard.getByTestId('project-suggest-delete-btn')).toBeVisible();
  await rejectedCard.getByTestId('project-suggest-delete-btn').click();
  await expect(list.locator('.suggestion-card')).toHaveCount(0);
});

test('rejected scope swaps bulk actions to restore/delete and targets rejected only', async ({ page }) => {
  await gotoSuggestions(page);

  const list = page.getByTestId('project-suggest-list');
  const firstCard = list.locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();
  await firstCard.getByRole('button', { name: /^Accept$/ }).click();
  await handleProjectAcceptDialog(page, 'skip');

  const secondCard = list.locator('.suggestion-card').first();
  await secondCard.locator('.header-row').click();
  await secondCard.getByRole('button', { name: /^Reject$/ }).click();

  await page.getByTestId('project-suggest-scope').getByText('Rejected').click();
  await expect(list.locator('.suggestion-card')).toHaveCount(1);

  await expect(page.getByTestId('project-suggestions-accept-all-btn')).toHaveCount(0);
  await expect(page.getByTestId('project-suggestions-reject-all-btn')).toHaveCount(0);
  await expect(page.getByTestId('project-suggestions-restore-all-btn')).toBeVisible();
  await expect(page.getByTestId('project-suggestions-delete-all-btn')).toBeVisible();

  await page.getByTestId('project-suggestions-delete-all-btn').click();
  const deleteDialog = page.locator('mat-dialog-container');
  await expect(deleteDialog.getByRole('heading', { name: 'Delete rejected project suggestions' })).toBeVisible();
  await deleteDialog.getByRole('button', { name: 'Delete' }).click();

  await expect(list.locator('.suggestion-card')).toHaveCount(0);
  await page.getByTestId('project-suggest-scope').getByText('Accepted').click();
  await expect(list.locator('.suggestion-card')).toHaveCount(1);
  await expect(list.locator('.status').first()).toHaveText('accepted');
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

test('suggestions regression actions show summary and allow export', async ({ page }) => {
  await gotoSuggestions(page);

  await page.getByTestId('project-suggestions-run-regression-btn').click();
  await expect(page.getByTestId('regression-report-summary')).toBeVisible();
  await expect(page.getByTestId('regression-report-roots').locator('.regression-root')).toHaveCount(2);

  await page.getByTestId('project-suggestions-export-regression-btn').click();
  await expect(page.getByText(/Exported:/)).toBeVisible();
});

test('project suggestions accept all and reject all require confirmation dialog', async ({ page }) => {
  await gotoSuggestions(page);

  await expect(page.getByTestId('project-suggestions-actions')).toBeVisible();
  const list = page.getByTestId('project-suggest-list');
  await expect(list.locator('.suggestion-card')).toHaveCount(10);

  await page.getByTestId('project-suggestions-accept-all-btn').click();
  const acceptProjectDialog = page.locator('mat-dialog-container');
  await expect(acceptProjectDialog.getByRole('heading', { name: 'Accept all project suggestions' })).toBeVisible();
  await acceptProjectDialog.getByRole('button', { name: 'Cancel' }).click();
  await expect(list.locator('.suggestion-card')).toHaveCount(10);

  await page.getByTestId('project-suggestions-reject-all-btn').click();
  const rejectProjectDialog = page.locator('mat-dialog-container');
  await expect(rejectProjectDialog.getByRole('heading', { name: 'Reject all project suggestions' })).toBeVisible();
  await rejectProjectDialog.getByRole('button', { name: 'Reject' }).click();
  await expect(list.locator('.suggestion-card')).toHaveCount(0);
});

test('project accept dialog allows choosing heuristics or AI action', async ({ page }) => {
  await gotoSuggestions(page);

  const list = page.getByTestId('project-suggest-list');
  const firstCard = list.locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();
  await firstCard.getByRole('button', { name: /^Accept$/ }).click();
  await handleProjectAcceptDialog(page, 'heuristics');
  await expect(page.getByText(/Tag heuristics generated/)).toBeVisible();

  const nextCard = list.locator('.suggestion-card').first();
  await nextCard.locator('.header-row').click();
  await nextCard.getByRole('button', { name: /^Accept$/ }).click();
  await handleProjectAcceptDialog(page, 'ai');
  await expect(page.getByText('AI tag suggestions queued')).toBeVisible();
});

test('tag heuristics run is visible in scan status with progress', async ({ page }) => {
  await gotoSuggestions(page);

  const list = page.getByTestId('project-suggest-list');
  const firstCard = list.locator('.suggestion-card').first();
  const projectName = (await firstCard.getByTestId('project-name').innerText()).trim();

  await firstCard.locator('.header-row').click();
  await firstCard.getByRole('button', { name: /^Accept$/ }).click();
  await handleProjectAcceptDialog(page, 'heuristics');
  await expect(page.getByText(/Tag heuristics generated/)).toBeVisible();

  await page.getByRole('tab', { name: 'Scan' }).click();
  const section = page.getByTestId('tag-heuristics-section');
  await expect(section).toBeVisible();

  const firstRun = section.locator('[data-testid^="tag-heuristics-run-"]').first();
  await expect(firstRun).toBeVisible();
  await expect(firstRun).toContainText(projectName);
  await expect(firstRun).toContainText('Progress');
  await expect(firstRun).toContainText('100%');

  const clearBtn = firstRun.locator('[data-testid^="tag-heuristics-clear-btn-"]');
  await expect(clearBtn).toBeVisible();
  await clearBtn.click();

  const clearDialog = page.locator('mat-dialog-container');
  await expect(clearDialog.getByRole('heading', { name: 'Clear completed tag heuristics run' })).toBeVisible();
  await clearDialog.getByRole('button', { name: 'Clear' }).click();

  await expect(section.locator('[data-testid^="tag-heuristics-run-"]')).toHaveCount(0);
  await expect(section).toContainText('No tag heuristics runs yet.');
});

test('tag suggestions accept all requires confirmation dialog', async ({ page }) => {
  await gotoSuggestions(page);

  const tagScope = page.locator('app-tag-suggestion-list');
  await expect(tagScope.locator('.status[data-status="pending"]')).toHaveCount(4);

  await page.getByTestId('tag-suggestions-accept-all-btn').click();
  const firstTagDialog = page.locator('mat-dialog-container').last();
  await expect(firstTagDialog.getByRole('heading', { name: 'Accept all tag suggestions' })).toBeVisible();
  await firstTagDialog.getByRole('button', { name: 'Cancel' }).click();
  await expect(tagScope.locator('.status[data-status="pending"]')).toHaveCount(4);

  await page.getByTestId('tag-suggestions-accept-all-btn').click();
  const secondTagDialog = page.locator('mat-dialog-container').last();
  await expect(secondTagDialog.getByRole('heading', { name: 'Accept all tag suggestions' })).toBeVisible();
  await secondTagDialog.getByRole('button', { name: 'Accept' }).click();
  await expect(page.locator('mat-dialog-container')).toHaveCount(0);
});
