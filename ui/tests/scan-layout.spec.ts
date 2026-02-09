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
