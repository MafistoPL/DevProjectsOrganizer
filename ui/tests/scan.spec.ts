import { test, expect } from '@playwright/test';

test('scan view renders key sections and actions', async ({ page }) => {
  await page.goto('/scan');

  await expect(page.getByRole('heading', { name: 'Scan' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Start scan' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Manage roots' })).toBeVisible();

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
