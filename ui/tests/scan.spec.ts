import { test, expect } from '@playwright/test';

test('scan view renders key sections and actions', async ({ page }) => {
  await page.goto('/scan');

  await expect(page.getByRole('heading', { name: 'Scan' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Start scan' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Manage roots' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Rescan selected roots' })).toBeVisible();

  const rootsCard = page.getByTestId('roots-card');
  const statusCard = page.getByTestId('status-card');
  const liveResultsCard = page.getByTestId('live-results-card');

  await expect(rootsCard).toBeVisible();
  await expect(statusCard).toBeVisible();
  await expect(liveResultsCard).toBeVisible();

  const rootsBox = await rootsCard.boundingBox();
  const statusBox = await statusCard.boundingBox();
  const liveResultsBox = await liveResultsCard.boundingBox();

  if (!rootsBox || !statusBox || !liveResultsBox) {
    throw new Error('Failed to read scan layout bounds');
  }

  expect(Math.abs(rootsBox.y - statusBox.y)).toBeLessThanOrEqual(2);
  expect(liveResultsBox.y).toBeGreaterThan(rootsBox.y + rootsBox.height - 2);
});

test('scan view is responsive: two columns on desktop and one column on mobile', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/scan');

  const rootsCard = page.getByTestId('roots-card');
  const statusCard = page.getByTestId('status-card');

  const desktopRoots = await rootsCard.boundingBox();
  const desktopStatus = await statusCard.boundingBox();
  if (!desktopRoots || !desktopStatus) {
    throw new Error('Failed to read desktop layout bounds');
  }

  expect(desktopStatus.x).toBeGreaterThan(desktopRoots.x + desktopRoots.width - 4);

  await page.setViewportSize({ width: 780, height: 900 });

  const mobileRoots = await rootsCard.boundingBox();
  const mobileStatus = await statusCard.boundingBox();
  if (!mobileRoots || !mobileStatus) {
    throw new Error('Failed to read mobile layout bounds');
  }

  expect(Math.abs(mobileRoots.x - mobileStatus.x)).toBeLessThanOrEqual(4);
  expect(mobileStatus.y).toBeGreaterThan(mobileRoots.y + mobileRoots.height - 2);
});

test('rescan selected roots queues scan only for checked roots', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      'mockRoots',
      JSON.stringify([
        {
          id: 'root-a',
          path: 'D:\\code',
          status: 'scanned',
          projectCount: 2,
          ongoingSuggestionCount: 0,
          lastScanState: null,
          lastScanAt: null,
          lastScanFiles: null
        },
        {
          id: 'root-b',
          path: 'C:\\src',
          status: 'changed',
          projectCount: 1,
          ongoingSuggestionCount: 0,
          lastScanState: null,
          lastScanAt: null,
          lastScanFiles: null
        }
      ])
    );
    localStorage.setItem('mockScans', JSON.stringify([]));
  });

  await page.goto('/scan');

  const rescanButton = page.getByRole('button', { name: 'Rescan selected roots' });
  await expect(rescanButton).toBeDisabled();

  await expect(page.getByTestId('scan-root-depth-field-root-b')).toHaveCount(0);
  await page.getByTestId('scan-root-select-root-b').click();
  await expect(page.getByTestId('scan-root-depth-field-root-b')).toBeVisible();
  await page.getByTestId('scan-root-depth-input-root-b').fill('3');
  await expect(rescanButton).toBeEnabled();
  await rescanButton.click();

  const statusCard = page.getByTestId('status-card');
  await expect(statusCard).toContainText('C:\\src');
  await expect(statusCard).toContainText('depth-3');
  await expect(statusCard).not.toContainText('D:\\code');
});
