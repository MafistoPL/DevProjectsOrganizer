import { expect, test } from '@playwright/test';

test('tags page supports add/edit/delete CRUD flow', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      'mockTags',
      JSON.stringify([
        {
          id: 'tag-1',
          name: 'csharp',
          isSystem: true,
          projectCount: 1,
          createdAt: '2026-02-13T10:00:00.000Z',
          updatedAt: '2026-02-13T10:00:00.000Z'
        }
      ])
    );

    localStorage.setItem(
      'mockSuggestions',
      JSON.stringify([
        {
          id: 's-1',
          scanSessionId: 'scan-1',
          rootPath: 'D:\\code',
          name: 'dotnet-api',
          score: 0.88,
          kind: 'ProjectRoot',
          path: 'D:\\code\\dotnet-api',
          reason: '.sln + csproj markers',
          extensionsSummary: 'cs=142',
          markers: ['.sln', '.csproj'],
          techHints: ['csharp'],
          createdAt: '2026-02-10T10:00:00.000Z',
          status: 'Accepted'
        }
      ])
    );

    localStorage.setItem(
      'mockProjectTags',
      JSON.stringify([
        {
          projectId: 'project-s-1',
          tagId: 'tag-1'
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
  const deleteDialog = page.locator('mat-dialog-container');
  await expect(deleteDialog.getByRole('heading', { name: 'Delete tag' })).toBeVisible();
  const confirmDeleteTagBtn = deleteDialog.getByTestId('tag-delete-confirm-btn');
  await expect(confirmDeleteTagBtn).toBeDisabled();
  await deleteDialog.getByTestId('tag-delete-confirm-input').fill('wrong');
  await expect(confirmDeleteTagBtn).toBeDisabled();
  await deleteDialog.getByTestId('tag-delete-confirm-input').fill('cpp');
  await expect(confirmDeleteTagBtn).toBeEnabled();
  await confirmDeleteTagBtn.click();
  await expect(page.getByTestId('tag-row').filter({ hasText: 'cpp' })).toHaveCount(0);
});

test('system tag hides delete and project count bubble opens modal', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      'mockTags',
      JSON.stringify([
        {
          id: 'tag-system-1',
          name: 'csharp',
          isSystem: true,
          projectCount: 1,
          createdAt: '2026-02-13T10:00:00.000Z',
          updatedAt: '2026-02-13T10:00:00.000Z'
        }
      ])
    );

    localStorage.setItem(
      'mockSuggestions',
      JSON.stringify([
        {
          id: 's-1',
          scanSessionId: 'scan-1',
          rootPath: 'D:\\code',
          name: 'dotnet-api',
          score: 0.88,
          kind: 'ProjectRoot',
          path: 'D:\\code\\dotnet-api',
          reason: '.sln + csproj markers',
          extensionsSummary: 'cs=142',
          markers: ['.sln', '.csproj'],
          techHints: ['csharp'],
          createdAt: '2026-02-10T10:00:00.000Z',
          status: 'Accepted'
        }
      ])
    );

    localStorage.setItem(
      'mockProjectTags',
      JSON.stringify([
        {
          projectId: 'project-s-1',
          tagId: 'tag-system-1'
        }
      ])
    );
  });

  await page.goto('/tags');

  const row = page.getByTestId('tag-row').first();
  await expect(row).toContainText('Seeded');
  await expect(row.getByTestId('tag-delete-btn')).toHaveCount(0);

  await row.getByTestId('tag-project-count-btn-tag-system-1').click();
  const dialog = page.locator('mat-dialog-container');
  await expect(dialog.getByRole('heading', { name: /Projects with tag/ })).toBeVisible();
  await expect(dialog.getByTestId('tag-project-row')).toHaveCount(1);
});

test('tags page applies latest heuristics to all projects with confirmation', async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      'mockTags',
      JSON.stringify([
        {
          id: 'tag-1',
          name: 'csharp',
          isSystem: true,
          projectCount: 1,
          createdAt: '2026-02-13T10:00:00.000Z',
          updatedAt: '2026-02-13T10:00:00.000Z'
        },
        {
          id: 'tag-2',
          name: 'native',
          isSystem: true,
          projectCount: 0,
          createdAt: '2026-02-13T10:00:00.000Z',
          updatedAt: '2026-02-13T10:00:00.000Z'
        }
      ])
    );

    localStorage.setItem(
      'mockSuggestions',
      JSON.stringify([
        {
          id: 's-1',
          scanSessionId: 'scan-1',
          rootPath: 'D:\\code',
          name: 'dotnet-api',
          score: 0.88,
          kind: 'ProjectRoot',
          path: 'D:\\code\\dotnet-api',
          reason: '.sln + csproj markers',
          extensionsSummary: 'cs=142',
          markers: ['.sln', '.csproj'],
          techHints: ['csharp'],
          createdAt: '2026-02-10T10:00:00.000Z',
          status: 'Accepted'
        },
        {
          id: 's-2',
          scanSessionId: 'scan-2',
          rootPath: 'D:\\code',
          name: 'cpp-tree',
          score: 0.82,
          kind: 'ProjectRoot',
          path: 'D:\\code\\cpp-tree',
          reason: '.vcxproj marker',
          extensionsSummary: 'cpp=38,h=4',
          markers: ['.vcxproj'],
          techHints: ['cpp', 'native'],
          createdAt: '2026-02-11T10:00:00.000Z',
          status: 'Accepted'
        }
      ])
    );
  });

  await page.goto('/tags');

  await page.getByTestId('tag-apply-heuristics-all-btn').click();
  const dialog = page.locator('mat-dialog-container').last();
  await expect(dialog.getByRole('heading', { name: 'Apply latest heuristics to all projects' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Run' }).click();

  await expect(page.getByTestId('tag-apply-heuristics-status')).toContainText('Processed 2/2');
  await expect(page.getByTestId('tag-heuristics-regression-summary')).toContainText('Projects analyzed: 2');
});
