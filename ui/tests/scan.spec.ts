import { test, expect } from '@playwright/test';

test('scan view matches snapshot', async ({ page }) => {
  await page.goto('/scan');
  await page.getByTestId('live-results-card').waitFor();
  await expect(page).toHaveScreenshot('scan-page.png', { fullPage: true });
});
