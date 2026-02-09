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
