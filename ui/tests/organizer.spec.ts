import { expect, test } from '@playwright/test';

test('project delete requires typed name confirmation', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      'mockSuggestions',
      JSON.stringify([
        {
          id: 's1',
          scanSessionId: 'scan-1',
          rootPath: 'D:\\code',
          name: 'dotnet-api',
          score: 0.88,
          kind: 'ProjectRoot',
          path: 'D:\\code\\dotnet-api',
          reason: 'markers: .sln',
          extensionsSummary: 'cs=10',
          markers: ['.sln'],
          techHints: ['csharp'],
          createdAt: '2026-02-15T10:00:00.000Z',
          status: 'Accepted'
        }
      ])
    );
  });

  await page.goto('/organizer');

  const deleteBtn = page.getByRole('button', { name: 'Delete' }).first();
  await expect(deleteBtn).toBeVisible();
  await deleteBtn.click();

  const dialog = page.locator('mat-dialog-container');
  await expect(dialog.getByRole('heading', { name: 'Delete project' })).toBeVisible();

  const confirmBtn = dialog.getByTestId('project-delete-confirm-btn');
  await expect(confirmBtn).toBeDisabled();

  await dialog.getByTestId('project-delete-confirm-input').fill('wrong-name');
  await expect(confirmBtn).toBeDisabled();

  await dialog.getByTestId('project-delete-confirm-input').fill('dotnet-api');
  await expect(confirmBtn).toBeEnabled();
  await confirmBtn.click();

  await expect(page.getByText('No accepted projects yet.')).toBeVisible();
});
