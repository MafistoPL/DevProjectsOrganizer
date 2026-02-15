import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TagSuggestionListComponent } from './tag-suggestion-list.component';
import { TagSuggestionsService } from '../../../services/tag-suggestions.service';

describe('TagSuggestionListComponent', () => {
  let fixture: ComponentFixture<TagSuggestionListComponent>;
  let setPendingSpy: ReturnType<typeof vi.fn>;
  let dialogOpenSpy: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    const suggestionsServiceMock = {
      items$: new BehaviorSubject<any[]>([
        {
          id: 'ts-1',
          projectId: 'p-1',
          projectName: 'dotnet-api',
          tagId: 't-1',
          tagName: 'csharp',
          type: 'assignexisting',
          source: 'heuristic',
          confidence: 0.86,
          reason: 'marker:.csproj',
          createdAt: '2026-02-14T10:00:00.000Z',
          status: 'pending'
        },
        {
          id: 'ts-2',
          projectId: 'p-2',
          projectName: 'frontend-app',
          tagId: 't-2',
          tagName: 'angular',
          type: 'assignexisting',
          source: 'heuristic',
          confidence: 0.72,
          reason: 'marker:package.json',
          createdAt: '2026-02-13T10:00:00.000Z',
          status: 'pending'
        },
        {
          id: 'ts-3',
          projectId: 'p-3',
          projectName: 'legacy-tools',
          tagId: 't-3',
          tagName: 'archive',
          type: 'assignexisting',
          source: 'heuristic',
          confidence: 0.61,
          reason: 'hint:legacy',
          createdAt: '2026-02-12T10:00:00.000Z',
          status: 'rejected'
        }
      ]),
      setStatus: vi.fn().mockResolvedValue(undefined),
      setPendingStatusForAll: vi.fn().mockResolvedValue(1)
    };
    setPendingSpy = suggestionsServiceMock.setPendingStatusForAll;

    const matDialogMock = {
      open: vi.fn()
    };
    dialogOpenSpy = matDialogMock.open;

    const snackBarMock = {
      open: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [TagSuggestionListComponent],
      providers: [
        provideNoopAnimations(),
        { provide: TagSuggestionsService, useValue: suggestionsServiceMock },
        { provide: MatDialog, useValue: matDialogMock },
        { provide: MatSnackBar, useValue: snackBarMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TagSuggestionListComponent);
    fixture.detectChanges();
  });

  it('accept all runs only when confirmation is accepted', async () => {
    dialogOpenSpy.mockReturnValue({ afterClosed: () => of(true) });

    const button: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="tag-suggestions-accept-all-btn"]'
    );
    button.click();
    await fixture.whenStable();

    expect(dialogOpenSpy).toHaveBeenCalled();
    expect(setPendingSpy).toHaveBeenCalledWith('accepted');
  });

  it('reject all does not run when confirmation is cancelled', async () => {
    dialogOpenSpy.mockReturnValue({ afterClosed: () => of(false) });

    const button: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="tag-suggestions-reject-all-btn"]'
    );
    button.click();
    await fixture.whenStable();

    expect(dialogOpenSpy).toHaveBeenCalled();
    expect(setPendingSpy).not.toHaveBeenCalled();
  });

  it('supports searching and sorting by selected key and direction', () => {
    fixture.componentInstance.searchTerm = 'front';
    fixture.componentInstance.sortKey = 'projectName';
    fixture.componentInstance.sortDir = 'asc';

    expect(fixture.componentInstance.visibleItems).toHaveLength(1);
    expect(fixture.componentInstance.visibleItems[0].projectName).toBe('frontend-app');

    fixture.componentInstance.searchTerm = '';
    fixture.componentInstance.sortKey = 'tagName';
    fixture.componentInstance.sortDir = 'desc';

    expect(fixture.componentInstance.visibleItems[0].tagName).toBe('csharp');
  });

  it('filters by selected scope', () => {
    fixture.componentInstance.scope = 'rejected';
    expect(fixture.componentInstance.visibleItems).toHaveLength(1);
    expect(fixture.componentInstance.visibleItems[0].status).toBe('rejected');
  });

  it('disables search and resets to newest-first when sort switches to created date', () => {
    fixture.componentInstance.searchTerm = 'dotnet';
    fixture.componentInstance.sortDir = 'asc';

    fixture.componentInstance.onSortKeyChange('createdAt');

    expect(fixture.componentInstance.isSearchEnabled).toBe(false);
    expect(fixture.componentInstance.searchTerm).toBe('');
    expect(fixture.componentInstance.sortDir).toBe('desc');
    expect(fixture.componentInstance.visibleItems[0].id).toBe('ts-1');
  });

  it('searches by the selected sort field only', () => {
    fixture.componentInstance.scope = 'pending';
    fixture.componentInstance.sortKey = 'projectName';
    fixture.componentInstance.searchTerm = 'angular';
    expect(fixture.componentInstance.visibleItems).toHaveLength(0);

    fixture.componentInstance.sortKey = 'tagName';
    expect(fixture.componentInstance.visibleItems).toHaveLength(1);
    expect(fixture.componentInstance.visibleItems[0].tagName).toBe('angular');
  });
});
