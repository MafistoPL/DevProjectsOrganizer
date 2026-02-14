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
});
