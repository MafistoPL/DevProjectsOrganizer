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

test('project organizer allows editing project description', async ({ page }) => {
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

  const editButton = page.getByRole('button', { name: 'Edit description' }).first();
  await expect(editButton).toBeVisible();
  await editButton.click();

  const input = page.getByTestId('project-description-input-project-s1');
  await expect(input).toBeVisible();
  await input.fill('Updated project description');

  await page.getByTestId('project-save-description-btn-project-s1').click();
  await expect(page.getByText('Updated project description')).toBeVisible();
});

test('project organizer allows attaching and detaching existing tags', async ({ page }) => {
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

    localStorage.setItem(
      'mockTags',
      JSON.stringify([
        {
          id: 'tag-csharp',
          name: 'csharp',
          isSystem: true,
          createdAt: '2026-02-15T10:00:00.000Z',
          updatedAt: '2026-02-15T10:00:00.000Z'
        },
        {
          id: 'tag-backend',
          name: 'backend',
          isSystem: false,
          createdAt: '2026-02-15T10:00:00.000Z',
          updatedAt: '2026-02-15T10:00:00.000Z'
        }
      ])
    );

    localStorage.setItem(
      'mockProjectTags',
      JSON.stringify([
        {
          projectId: 'project-s1',
          tagId: 'tag-csharp'
        }
      ])
    );
  });

  await page.goto('/organizer');

  await page.getByTestId('project-attach-tag-select-project-s1').click();
  await page.getByRole('option', { name: 'backend' }).click();
  await page.getByTestId('project-attach-tag-btn-project-s1').click();

  const detachBackend = page.getByTestId('project-detach-tag-btn-project-s1-tag-backend');
  await expect(detachBackend).toBeVisible();
  await detachBackend.click();

  await expect(detachBackend).toHaveCount(0);
});
