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

const setMockRegressionReportError = async (page: Page, message: string) => {
  await page.addInitScript((errorMessage) => {
    localStorage.setItem('mockRegressionReportError', errorMessage);
  }, message);
};

const setMockRegressionReportPayload = async (page: Page, payload: unknown) => {
  await page.addInitScript((reportPayload) => {
    localStorage.setItem('mockRegressionReportPayload', JSON.stringify(reportPayload));
  }, payload);
};

const setMockTagSuggestions = async (page: Page, items: unknown[]) => {
  await page.addInitScript((mockItems) => {
    localStorage.setItem('mockTagSuggestions', JSON.stringify(mockItems));
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

const expectHeadingInViewport = async (page: Page, heading: string) => {
  const title = page.getByRole('heading', { name: heading });
  await expect(title).toBeVisible();
  const tabs = page.locator('nav.tabs');
  await expect(tabs).toBeVisible();

  const bounds = await title.boundingBox();
  const tabsBounds = await tabs.boundingBox();
  if (!bounds) {
    throw new Error(`Missing heading bounds: ${heading}`);
  }
  if (!tabsBounds) {
    throw new Error('Missing tabs bounds');
  }

  expect(bounds.y).toBeGreaterThanOrEqual(tabsBounds.y + tabsBounds.height + 4);
  expect(bounds.y).toBeLessThan(240);
};

const expectRegressionPanelInShellViewport = async (page: Page) => {
  const shellViewport = page.locator('mat-tab-nav-panel.content-inner');
  await expect(shellViewport).toBeVisible();

  await expect
    .poll(async () => {
      return await page.evaluate(() => {
        const panel = document.querySelector('[data-testid="regression-panel"]');
        const summary = document.querySelector('[data-testid="regression-report-summary"]');
        const error = document.querySelector('[data-testid="regression-report-error"]');
        const target = summary ?? error ?? panel;
        const shell = document.querySelector('mat-tab-nav-panel.content-inner');
        if (!(target instanceof HTMLElement) || !(shell instanceof HTMLElement)) {
          return false;
        }

        const panelRect = target.getBoundingClientRect();
        const shellRect = shell.getBoundingClientRect();
        return panelRect.top >= shellRect.top - 2 && panelRect.top < shellRect.bottom - 36;
      });
    })
    .toBe(true);
};

const getBottomGapToShell = async (page: Page, selector: string): Promise<number | null> => {
  return await page.evaluate(async (targetSelector) => {
    const shell = document.querySelector('mat-tab-nav-panel.content-inner');
    const target = document.querySelector(targetSelector);
    if (!(shell instanceof HTMLElement) || !(target instanceof HTMLElement)) {
      return null;
    }

    shell.scrollTop = shell.scrollHeight;
    await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));

    const shellRect = shell.getBoundingClientRect();
    const targetRect = target.getBoundingClientRect();
    return Number((shellRect.bottom - targetRect.bottom).toFixed(2));
  }, selector);
};

const getBottomGapToListViewport = async (page: Page, testId: string): Promise<number | null> => {
  return await page.evaluate(async (id) => {
    const list = document.querySelector(`[data-testid="${id}"]`);
    if (!(list instanceof HTMLElement)) {
      return null;
    }

    const cards = Array.from(list.querySelectorAll('.suggestion-card')).filter(
      (el): el is HTMLElement => el instanceof HTMLElement
    );
    if (cards.length === 0) {
      return null;
    }

    list.scrollTop = list.scrollHeight;
    await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));

    const listRect = list.getBoundingClientRect();
    const lastRect = cards[cards.length - 1].getBoundingClientRect();
    return Number((listRect.bottom - lastRect.bottom).toFixed(2));
  }, testId);
};

const getBottomSpacerGeometry = async (page: Page): Promise<{
  reportBottomGap: number;
  spacerVisiblePx: number;
  spacerTopToShellBottom: number;
} | null> => {
  return await page.evaluate(async () => {
    const shell = document.querySelector('mat-tab-nav-panel.content-inner');
    const report = document.querySelector('[data-testid=\"regression-panel\"]');
    const roots = document.querySelector('[data-testid=\"regression-report-roots\"]');
    const summary = document.querySelector('[data-testid=\"regression-report-summary\"]');
    const error = document.querySelector('[data-testid=\"regression-report-error\"]');
    const spacer = document.querySelector('[data-testid=\"suggestions-bottom-spacer\"]');
    if (!(shell instanceof HTMLElement) || !(spacer instanceof HTMLElement)) {
      return null;
    }

    for (let i = 0; i < 6; i += 1) {
      shell.scrollTop = shell.scrollHeight;
      await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
    }

    const shellRect = shell.getBoundingClientRect();
    const spacerRect = spacer.getBoundingClientRect();
    const reportTarget =
      (roots instanceof HTMLElement ? roots : null) ??
      (summary instanceof HTMLElement ? summary : null) ??
      (error instanceof HTMLElement ? error : null) ??
      (report instanceof HTMLElement ? report : null);
    const reportRect = reportTarget instanceof HTMLElement ? reportTarget.getBoundingClientRect() : null;

    const spacerVisiblePx = Math.max(
      0,
      Math.min(shellRect.bottom, spacerRect.bottom) - Math.max(shellRect.top, spacerRect.top)
    );

    return {
      reportBottomGap: reportRect ? Number((shellRect.bottom - reportRect.bottom).toFixed(2)) : 0,
      spacerVisiblePx: Number(spacerVisiblePx.toFixed(2)),
      spacerTopToShellBottom: Number((shellRect.bottom - spacerRect.top).toFixed(2))
    };
  });
};

const buildLongMockSuggestions = (count: number) => {
  return Array.from({ length: count }, (_, idx) => ({
    id: `long-${idx + 1}`,
    scanSessionId: `scan-long-${idx + 1}`,
    rootPath: 'D:\\bulk',
    name: `bulk-project-${String(idx + 1).padStart(3, '0')}`,
    score: 0.5 + ((idx % 40) * 0.01),
    kind: idx % 2 === 0 ? 'ProjectRoot' : 'Collection',
    path: `D:\\bulk\\project-${idx + 1}`,
    reason: 'generated for regression panel viewport test',
    extensionsSummary: 'cs=10,json=2',
    markers: ['.sln'],
    techHints: ['csharp'],
    createdAt: new Date(Date.UTC(2025, 0, 1, 12, 0, idx)).toISOString(),
    status: 'Pending'
  }));
};

const buildLongMockTagSuggestions = (count: number) => {
  return Array.from({ length: count }, (_, idx) => ({
    id: `tag-long-${idx + 1}`,
    projectId: `project-long-${idx + 1}`,
    projectName: `project-long-${String(idx + 1).padStart(3, '0')}`,
    tagId: `tag-${(idx % 10) + 1}`,
    tagName: `tag-${(idx % 10) + 1}`,
    type: 'AssignExisting',
    source: 'Heuristic',
    confidence: 0.55 + ((idx % 40) * 0.01),
    reason: `generated-tag-suggestion-${idx + 1}`,
    createdAt: new Date(Date.UTC(2025, 0, 1, 12, 0, idx)).toISOString(),
    status: 'Pending'
  }));
};

const buildTallMockRegressionReport = (rootsCount: number) => {
  const roots = Array.from({ length: rootsCount }, (_, idx) => ({
    rootPath: `D:\\bulk\\root-${String(idx + 1).padStart(2, '0')}`,
    snapshotScanSessionId: `scan-root-${idx + 1}`,
    snapshotPath: `C:\\Users\\Mock\\AppData\\Roaming\\DevProjectsOrganizer\\scans\\scan-root-${idx + 1}.json`,
    baselineAcceptedCount: 10 + (idx % 3),
    baselineRejectedCount: 2 + (idx % 2),
    acceptedMissingCount: idx % 2,
    rejectedMissingCount: (idx + 1) % 2,
    addedCount: 5 + (idx % 4),
    acceptedMissingPaths: [],
    rejectedMissingPaths: []
  }));

  return {
    rootsAnalyzed: rootsCount,
    baselineAcceptedCount: roots.reduce((acc, item) => acc + item.baselineAcceptedCount, 0),
    baselineRejectedCount: roots.reduce((acc, item) => acc + item.baselineRejectedCount, 0),
    acceptedMissingCount: roots.reduce((acc, item) => acc + item.acceptedMissingCount, 0),
    rejectedMissingCount: roots.reduce((acc, item) => acc + item.rejectedMissingCount, 0),
    addedCount: roots.reduce((acc, item) => acc + item.addedCount, 0),
    roots
  };
};

const handleProjectAcceptDialog = async (
  page: Page,
  action: 'skip' | 'heuristics' | 'ai' = 'skip',
  projectName?: string
) => {
  const dialog = page.locator('mat-dialog-container').last();
  await expect(dialog.getByRole('heading', { name: 'Accept project suggestion' })).toBeVisible();

  if (projectName !== undefined) {
    await dialog.getByTestId('project-accept-name-input').fill(projectName);
  }

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

test('suggestions archive export shows exported snackbar and open folder action', async ({ page }) => {
  await gotoSuggestions(page);

  await page.getByTestId('project-suggest-export-archive').click();
  await expect(page.getByText(/Exported \d+ archived suggestion\(s\)/)).toBeVisible();

  await page.getByTestId('project-suggest-open-archive-folder').click();
  await expect(page.getByText(/Opened:/)).toBeVisible();
});

test('tag suggestions place scope row below search and support grid card resize', async ({ page }) => {
  await gotoSuggestions(page);

  const search = page.getByTestId('tag-suggest-search');
  const scope = page.getByTestId('tag-suggest-scope');
  await expect(search).toBeVisible();
  await expect(scope).toBeVisible();

  const searchBox = await search.boundingBox();
  const scopeBox = await scope.boundingBox();
  if (!searchBox || !scopeBox) {
    throw new Error('Missing tag suggestion control bounds');
  }

  expect(scopeBox.y).toBeGreaterThan(searchBox.y + searchBox.height - 2);

  const list = page.getByTestId('tag-suggest-list');
  await expect(page.getByTestId('tag-suggest-grid-size')).toBeDisabled();
  await page.getByTestId('tag-suggest-layout').getByText('Grid').click();
  await expect(list).toHaveClass(/layout-grid/);
  await expect(page.getByTestId('tag-suggest-grid-size')).toBeEnabled();

  const firstCard = list.locator('.suggestion-card').first();
  const before = await firstCard.boundingBox();
  if (!before) {
    throw new Error('Missing initial tag card bounds');
  }

  await page.getByTestId('tag-suggest-grid-size').fill('130');
  const after = await firstCard.boundingBox();
  if (!after) {
    throw new Error('Missing resized tag card bounds');
  }

  expect(after.height).toBeGreaterThan(before.height + 20);
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
  await expectRegressionPanelInShellViewport(page);

  await page.getByTestId('project-suggestions-export-regression-btn').click();
  await expect(page.getByText(/Exported:/)).toBeVisible();
});

test('regression panel container wraps rendered report content', async ({ page }) => {
  await gotoSuggestions(page);
  await page.getByTestId('project-suggestions-run-regression-btn').click();
  await expect(page.getByTestId('regression-report-summary')).toBeVisible();

  const geometry = await page.evaluate(() => {
    const panel = document.querySelector('[data-testid="regression-panel"]');
    const summary = document.querySelector('[data-testid="regression-report-summary"]');
    if (!(panel instanceof HTMLElement) || !(summary instanceof HTMLElement)) {
      return null;
    }

    const panelRect = panel.getBoundingClientRect();
    const summaryRect = summary.getBoundingClientRect();
    return {
      panelHeight: Number(panelRect.height.toFixed(2)),
      panelBottom: Number(panelRect.bottom.toFixed(2)),
      summaryBottom: Number(summaryRect.bottom.toFixed(2))
    };
  });

  if (!geometry) {
    throw new Error('Cannot measure regression panel container geometry');
  }

  expect(geometry.panelHeight).toBeGreaterThanOrEqual(80);
  expect(geometry.panelBottom).toBeGreaterThanOrEqual(geometry.summaryBottom - 2);
});

test('regression report is placed in GUI viewport for long suggestions list', async ({ page }) => {
  await setMockSuggestions(page, buildLongMockSuggestions(120));
  await gotoSuggestions(page);

  const shellViewport = page.locator('mat-tab-nav-panel.content-inner');
  await expect(shellViewport).toBeVisible();
  await expect
    .poll(async () => {
      return await shellViewport.evaluate((el) => el.scrollTop);
    })
    .toBe(0);

  await page.getByTestId('project-suggestions-run-regression-btn').click();
  await expect(page.getByTestId('regression-report-summary')).toBeVisible();
  await expectRegressionPanelInShellViewport(page);

  const scrolledTop = await shellViewport.evaluate((el) => el.scrollTop);
  expect(scrolledTop).toBeGreaterThan(200);
});

test('run regression report renders immediately without extra GUI interaction', async ({ page }) => {
  await page.setViewportSize({ width: 1200, height: 760 });
  await setMockSuggestions(page, buildLongMockSuggestions(120));
  await gotoSuggestions(page);

  const shellViewport = page.locator('mat-tab-nav-panel.content-inner');
  const initialTop = await shellViewport.evaluate((el) => el.scrollTop);

  await page.getByTestId('project-suggestions-run-regression-btn').click();
  await expect(page.getByTestId('regression-report-summary')).toBeVisible({ timeout: 4000 });
  await expectRegressionPanelInShellViewport(page);

  const finalTop = await shellViewport.evaluate((el) => el.scrollTop);
  expect(finalTop).toBeGreaterThan(initialTop + 120);
});

test('regression report error is placed in GUI viewport for long suggestions list', async ({ page }) => {
  await setMockSuggestions(page, buildLongMockSuggestions(120));
  await setMockRegressionReportError(page, 'Forced regression error for viewport test');
  await gotoSuggestions(page);

  await page.getByTestId('project-suggestions-run-regression-btn').click();
  await expect(page.getByTestId('regression-report-error')).toBeVisible();
  await expect(page.getByTestId('regression-report-error')).toContainText(
    'Forced regression error for viewport test'
  );
  await expectRegressionPanelInShellViewport(page);
});

test('suggestions page keeps bottom margin without regression report', async ({ page }) => {
  await gotoSuggestions(page);
  await expect(page.getByTestId('regression-panel')).toHaveCount(0);

  const gap = await getBottomGapToShell(page, '[data-testid="suggestions-card-grid"]');
  if (gap == null) {
    throw new Error('Cannot compute bottom gap for suggestions card grid');
  }

  expect(gap).toBeGreaterThanOrEqual(16);
});

test('suggestions page keeps bottom margin with regression report', async ({ page }) => {
  await gotoSuggestions(page);
  await page.getByTestId('project-suggestions-run-regression-btn').click();
  await expect(page.getByTestId('regression-report-summary')).toBeVisible();

  const gap = await getBottomGapToShell(page, '[data-testid="regression-panel"]');
  if (gap == null) {
    throw new Error('Cannot compute bottom gap for regression panel');
  }

  expect(gap).toBeGreaterThanOrEqual(16);
});

test('suggestions bottom spacer stays visible with tall regression report', async ({ page }) => {
  await page.setViewportSize({ width: 1536, height: 973 });
  await setMockSuggestions(page, buildLongMockSuggestions(120));
  await setMockRegressionReportPayload(page, buildTallMockRegressionReport(24));
  await gotoSuggestions(page);

  await page.getByTestId('project-suggestions-run-regression-btn').click();
  await expect(page.getByTestId('regression-report-summary')).toBeVisible();
  await expect(page.getByTestId('regression-report-roots').locator('.regression-root')).toHaveCount(24);

  const geometry = await getBottomSpacerGeometry(page);
  if (!geometry) {
    throw new Error('Cannot measure suggestions bottom spacer geometry');
  }

  expect(geometry.reportBottomGap).toBeGreaterThanOrEqual(16);
  expect(geometry.spacerVisiblePx).toBeGreaterThanOrEqual(12);
  expect(geometry.spacerTopToShellBottom).toBeGreaterThan(0);
});

test('suggestions bottom spacer is visible without regression report in host-like viewport', async ({ page }) => {
  await page.setViewportSize({ width: 1536, height: 973 });
  await setMockSuggestions(page, buildLongMockSuggestions(120));
  await gotoSuggestions(page);

  const geometry = await getBottomSpacerGeometry(page);
  if (!geometry) {
    throw new Error('Cannot measure suggestions bottom spacer geometry without report');
  }

  expect(geometry.spacerVisiblePx).toBeGreaterThanOrEqual(12);
  expect(geometry.spacerTopToShellBottom).toBeGreaterThan(0);
});

test('suggestions lists keep inner bottom gap at end of scroll', async ({ page }) => {
  await page.setViewportSize({ width: 1200, height: 760 });
  await setMockSuggestions(page, buildLongMockSuggestions(80));
  await setMockTagSuggestions(page, buildLongMockTagSuggestions(80));
  await gotoSuggestions(page);

  const projectGap = await getBottomGapToListViewport(page, 'project-suggest-list');
  const tagGap = await getBottomGapToListViewport(page, 'tag-suggest-list');
  if (projectGap == null || tagGap == null) {
    throw new Error('Cannot compute list bottom gap');
  }

  expect(projectGap).toBeGreaterThanOrEqual(12);
  expect(tagGap).toBeGreaterThanOrEqual(12);
});

test('page headers stay visible across all tabs after regression and deep scroll', async ({ page }) => {
  await gotoSuggestions(page);

  await page.getByTestId('project-suggestions-run-regression-btn').click();
  await expect(page.getByTestId('regression-report-summary')).toBeVisible();

  await page.locator('app-suggestions-page .page').evaluate((el) => {
    el.scrollTop = el.scrollHeight;
  });
  await page.locator('mat-tab-nav-panel.content-inner').evaluate((el) => {
    el.scrollTop = el.scrollHeight;
  });

  await page.getByRole('tab', { name: 'Scan' }).click();
  await expectHeadingInViewport(page, 'Scan');

  await page.getByRole('tab', { name: 'Project Organizer' }).click();
  await expectHeadingInViewport(page, 'Project Organizer');

  await page.getByRole('tab', { name: 'Suggestions' }).click();
  await expectHeadingInViewport(page, 'Suggestions');

  await page.getByRole('tab', { name: 'Tags' }).click();
  await expectHeadingInViewport(page, 'Tags');

  await page.getByRole('tab', { name: 'Recent' }).click();
  await expectHeadingInViewport(page, 'Recent');
});

test('clicking active tab resets shell scroll and shows page heading', async ({ page }) => {
  await gotoSuggestions(page);

  await page.locator('mat-tab-nav-panel.content-inner').evaluate((el) => {
    el.scrollTop = 120;
  });

  await page.getByRole('tab', { name: 'Suggestions' }).click();

  await expect
    .poll(async () => {
      return await page.locator('mat-tab-nav-panel.content-inner').evaluate((el) => el.scrollTop);
    })
    .toBe(0);
  await expectHeadingInViewport(page, 'Suggestions');
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

test('project accept dialog allows editing project name before accept', async ({ page }) => {
  await gotoSuggestions(page);

  const list = page.getByTestId('project-suggest-list');
  const firstCard = list.locator('.suggestion-card').first();
  await firstCard.locator('.header-row').click();
  await firstCard.getByRole('button', { name: /^Accept$/ }).click();
  await handleProjectAcceptDialog(page, 'skip', 'dotnet-api-renamed');

  await page.getByRole('tab', { name: 'Project Organizer' }).click();
  await expect(page.getByText('dotnet-api-renamed')).toBeVisible();
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
