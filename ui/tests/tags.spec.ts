import { expect, test } from '@playwright/test';

test('tags page supports add/edit/delete CRUD flow', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      'mockTags',
      JSON.stringify([
        {
          id: 'tag-1',
          name: 'csharp',
          createdAt: '2026-02-13T10:00:00.000Z',
          updatedAt: '2026-02-13T10:00:00.000Z'
        }
      ])
    );
  });

  await page.goto('/tags');

  await expect(page.getByTestId('tag-row')).toHaveCount(1);
  await expect(page.getByTestId('tag-row').first()).toContainText('csharp');

  await page.getByTestId('tag-add-input').fill('cpp');
  await page.getByTestId('tag-add-btn').click();
  await expect(page.getByTestId('tag-row')).toHaveCount(2);
  await expect(page.getByTestId('tag-row').filter({ hasText: 'cpp' })).toHaveCount(1);

  const csharpRow = page.getByTestId('tag-row').filter({ hasText: 'csharp' }).first();
  await csharpRow.getByTestId('tag-edit-btn').click();
  await page.getByTestId('tag-edit-input').fill('dotnet');
  await page.getByTestId('tag-save-btn').click();
  await expect(page.getByTestId('tag-row').filter({ hasText: 'dotnet' })).toHaveCount(1);

  const cppRow = page.getByTestId('tag-row').filter({ hasText: 'cpp' }).first();
  await cppRow.getByTestId('tag-delete-btn').click();
  await expect(page.getByTestId('tag-row').filter({ hasText: 'cpp' })).toHaveCount(0);
});
