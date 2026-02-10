import { test, expect } from '@playwright/test';

test('Start scan validates root selection for root-based modes', async ({ page }) => {
  await page.goto('/scan');

  await page.getByRole('button', { name: 'Start scan' }).click();

  const dialog = page.getByRole('dialog', { name: 'Start scan' });
  await expect(dialog).toBeVisible();

  const startButton = dialog.getByRole('button', { name: 'Start' });
  await expect(startButton).toBeDisabled();

  const selectRoot = dialog.getByRole('combobox', { name: 'Select root' });
  await selectRoot.click();

  const option = page.getByRole('option').first();
  await option.click();

  await expect(startButton).toBeEnabled();
});

test('Whole computer mode does not require root selection', async ({ page }) => {
  await page.goto('/scan');

  await page.getByRole('button', { name: 'Start scan' }).click();

  const dialog = page.getByRole('dialog', { name: 'Start scan' });
  await expect(dialog).toBeVisible();

  await dialog.getByRole('radio', { name: 'Scan whole computer' }).click();

  await expect(dialog.getByRole('combobox', { name: 'Select root' })).toHaveCount(0);
  await expect(dialog.getByRole('button', { name: 'Start' })).toBeEnabled();
});

test('Changed roots mode filters root list', async ({ page }) => {
  await page.goto('/scan');

  await page.getByRole('button', { name: 'Start scan' }).click();
  const dialog = page.getByRole('dialog', { name: 'Start scan' });
  await expect(dialog).toBeVisible();

  await dialog.getByRole('radio', { name: 'Scan root changed since last scan' }).click();

  const selectRoot = dialog.getByRole('combobox', { name: 'Select root' });
  await expect(selectRoot).toBeVisible();

  await selectRoot.click();
  const options = page.getByRole('option');
  await expect(options).toHaveCount(1);

  const optionText = await options.first().innerText();
  expect(optionText.toLowerCase()).toContain('changed');
});
