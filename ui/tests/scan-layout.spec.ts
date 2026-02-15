import { test, expect } from '@playwright/test';

test('A equals B spacing between left edge and cards', async ({ page }) => {
  await page.goto('/scan');

  const roots = page.getByTestId('roots-card');
  const status = page.getByTestId('status-card');

  await expect(roots).toBeVisible();
  await expect(status).toBeVisible();

  const rootsBox = await roots.boundingBox();
  const statusBox = await status.boundingBox();

  if (!rootsBox || !statusBox) {
    throw new Error('Unable to read card bounds');
  }

  const a = rootsBox.x;
  const b = statusBox.x - (rootsBox.x + rootsBox.width);

  expect(Math.abs(a - b)).toBeLessThanOrEqual(2);
});

test('Live results items are not clipped', async ({ page }) => {
  await page.goto('/scan');

  const container = page.getByTestId('live-results-content');
  await expect(container).toBeVisible();

  const cards = container.locator('.suggestion-card');
  await expect(cards.first()).toBeVisible();

  // Ensure text label is fully visible (no horizontal clipping).
  const name = container.locator('[data-testid=\"project-name\"]', { hasText: 'dotnet-api' });
  await expect(name).toBeVisible();

  const isTextClipped = await name.evaluate((el) => {
    return el.scrollWidth > el.clientWidth;
  });
  expect(isTextClipped).toBeFalsy();

  // Scroll to bottom to ensure last item is fully visible.
  await container.evaluate((el) => {
    el.scrollTop = el.scrollHeight;
  });

  const last = cards.last();
  await expect(last).toBeVisible();

  const isLastFullyVisible = await container.evaluate((el) => {
    const lastCard = el.querySelector('.suggestion-card:last-child') as HTMLElement | null;
    if (!lastCard) {
      return false;
    }
    const containerRect = el.getBoundingClientRect();
    const lastRect = lastCard.getBoundingClientRect();
    return lastRect.bottom <= containerRect.bottom + 1;
  });

  expect(isLastFullyVisible).toBeTruthy();
});

test('Live results expands to fill available height', async ({ page }) => {
  await page.goto('/scan');

  const search = page.getByTestId('project-suggest-search');
  await search.fill('rust');

  const list = page.getByTestId('project-suggest-list').locator('[data-testid="project-name"]');
  await expect(list).toHaveCount(1);

  const card = page.getByTestId('live-results-card');
  await expect(card).toBeVisible();

  const box = await card.boundingBox();
  const viewport = page.viewportSize();

  if (!box || !viewport) {
    throw new Error('Unable to read live results bounds');
  }

  const gap = viewport.height - (box.y + box.height);
  expect(gap).toBeLessThanOrEqual(96);
});

test('Manage roots edit row keeps actions aligned with input', async ({ page }) => {
  await page.goto('/scan');

  await page.getByRole('button', { name: 'Manage roots' }).click();
  const dialog = page.locator('mat-dialog-container');
  await expect(dialog).toBeVisible();

  const editButton = dialog.getByRole('button', { name: 'Edit' }).first();
  await expect(editButton).toBeVisible();
  await editButton.click();

  const editField = dialog.locator('.edit-field input');
  const saveButton = dialog.getByRole('button', { name: 'Save' });

  await expect(editField).toBeVisible();
  await expect(saveButton).toBeVisible();

  const editBox = await editField.boundingBox();
  const saveBox = await saveButton.boundingBox();

  if (!editBox || !saveBox) {
    throw new Error('Unable to read edit layout bounds');
  }

  const editCenterY = editBox.y + editBox.height / 2;
  const saveCenterY = saveBox.y + saveBox.height / 2;

  expect(Math.abs(editCenterY - saveCenterY)).toBeLessThanOrEqual(10);
});

test('completed scan does not show stop button', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      'mockScans',
      JSON.stringify([
        {
          id: 'scan-completed-1',
          rootPath: 'D:\\code',
          mode: 'roots',
          state: 'Completed',
          disk: 'D:',
          currentPath: 'D:\\code\\project\\done.cpp',
          filesScanned: 12,
          totalFiles: 12,
          queueReason: null,
          outputPath: 'C:\\mock\\scan-completed-1.json'
        }
      ])
    );
  });

  await page.goto('/scan');

  const statusCard = page.getByTestId('status-card');
  await expect(statusCard).toContainText('Completed');
  await expect(page.getByTestId('scan-stop-btn-scan-completed-1')).toHaveCount(0);
  const clearBtn = page.getByTestId('scan-clear-btn-scan-completed-1');
  await expect(clearBtn).toBeVisible();

  await clearBtn.click();
  const dialog = page.locator('mat-dialog-container');
  await expect(dialog.getByRole('heading', { name: 'Clear completed scan' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Clear' }).click();

  await expect(page.locator('.scan-item')).toHaveCount(0);
  await expect(statusCard).toContainText('No active scans yet.');
});

test('active scan current path supports horizontal scroll for long paths', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      'mockScans',
      JSON.stringify([
        {
          id: 'scan-running-long-path',
          rootPath: 'D:\\old-projects',
          mode: 'roots',
          state: 'Running',
          disk: 'D:',
          currentPath:
            'D:\\z-pulpitu\\ProgrammingLearning\\one_drive\\Old_Projects\\Simple_Data_Structures\\One_Direction_Linked_List\\very\\long\\nested\\folder\\structure\\main.cpp',
          filesScanned: 77,
          totalFiles: 400,
          queueReason: null,
          outputPath: null,
          eta: '00:02:31'
        }
      ])
    );
  });

  await page.goto('/scan');

  const pathValue = page.getByTestId('scan-current-path-scan-running-long-path');
  await expect(pathValue).toBeVisible();

  const hasHorizontalOverflow = await pathValue.evaluate((element) => {
    return element.scrollWidth > element.clientWidth;
  });
  expect(hasHorizontalOverflow).toBeTruthy();
});

test('roots list shows project and pending badges with summary', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      'mockRoots',
      JSON.stringify([
        {
          id: 'root-1',
          path: 'D:\\code',
          status: 'scanned',
          projectCount: 8,
          ongoingSuggestionCount: 2,
          lastScanState: 'Completed',
          lastScanAt: '2026-02-12T18:24:00.000Z',
          lastScanFiles: 1284992
        }
      ])
    );
  });

  await page.goto('/scan');

  await expect(page.getByTestId('root-project-count').first()).toContainText('Projects');
  await expect(page.getByTestId('root-pending-count').first()).toContainText('Pending');
  await expect(page.getByTestId('root-last-summary').first()).toContainText('Last:');
  await expect(page.getByTestId('scan-root-path').first()).toContainText('D:\\code');
});

test('roots card path uses own horizontal scroll for long values', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      'mockRoots',
      JSON.stringify([
        {
          id: 'root-long',
          path: 'D:\\z-pulpitu\\ProgrammingLearning\\one_drive\\Old_Projects\\Very_Long_Root_Name\\More\\Segments\\Even_More\\Path',
          status: 'changed',
          projectCount: 3,
          ongoingSuggestionCount: 1,
          lastScanState: 'Completed',
          lastScanAt: '2026-02-12T18:24:00.000Z',
          lastScanFiles: 1284992
        }
      ])
    );
  });

  await page.goto('/scan');

  const rootPath = page.getByTestId('scan-root-path').first();
  await expect(rootPath).toBeVisible();

  const hasOverflow = await rootPath.evaluate((element) => element.scrollWidth > element.clientWidth);
  expect(hasOverflow).toBeTruthy();
});

test('manage roots dialog keeps horizontal scroll on path element, not whole dialog', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      'mockRoots',
      JSON.stringify([
        {
          id: 'root-long-dialog',
          path: 'D:\\z-pulpitu\\ProgrammingLearning\\one_drive\\Old_Projects\\Very_Long_Root_Name\\More\\Segments\\Even_More\\Path',
          status: 'changed',
          projectCount: 3,
          ongoingSuggestionCount: 1,
          lastScanState: null,
          lastScanAt: null,
          lastScanFiles: null
        }
      ])
    );
  });

  await page.goto('/scan');
  await page.getByRole('button', { name: 'Manage roots' }).click();

  const dialogContent = page.locator('mat-dialog-content.dialog-content');
  await expect(dialogContent).toBeVisible();

  const dialogHasOverflow = await dialogContent.evaluate((element) => element.scrollWidth > element.clientWidth);
  expect(dialogHasOverflow).toBeFalsy();

  const rootPath = page.getByTestId('manage-root-path').first();
  const pathHasOverflow = await rootPath.evaluate((element) => element.scrollWidth > element.clientWidth);
  expect(pathHasOverflow).toBeTruthy();
});
