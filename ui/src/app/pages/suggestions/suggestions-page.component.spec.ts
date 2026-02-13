import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { SuggestionsPageComponent } from './suggestions-page.component';
import { SuggestionsService } from '../../services/suggestions.service';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';

describe('SuggestionsPageComponent', () => {
  let fixture: ComponentFixture<SuggestionsPageComponent>;
  let dialogOpenSpy: ReturnType<typeof vi.fn>;
  let setPendingSpy: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    const suggestionsServiceMock = {
      items$: new BehaviorSubject([]),
      runRegressionReport: vi.fn().mockResolvedValue({
        rootsAnalyzed: 0,
        baselineAcceptedCount: 0,
        baselineRejectedCount: 0,
        acceptedMissingCount: 0,
        rejectedMissingCount: 0,
        addedCount: 0,
        roots: []
      }),
      exportRegressionReport: vi.fn().mockResolvedValue({
        path: 'C:\\mock\\report.json',
        rootsAnalyzed: 0
      }),
      setPendingStatusForAll: vi.fn().mockResolvedValue(3)
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
      imports: [SuggestionsPageComponent],
      providers: [
        provideNoopAnimations(),
        { provide: SuggestionsService, useValue: suggestionsServiceMock },
        { provide: MatDialog, useValue: matDialogMock },
        { provide: MatSnackBar, useValue: snackBarMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SuggestionsPageComponent);
    fixture.detectChanges();
  });

  it('runs project accept-all only after confirmation', async () => {
    dialogOpenSpy.mockReturnValue({ afterClosed: () => of(true) });
    const button: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="project-suggestions-accept-all-btn"]'
    );

    button.click();
    await fixture.whenStable();

    expect(dialogOpenSpy).toHaveBeenCalled();
    expect(setPendingSpy).toHaveBeenCalledWith('accepted');
  });

  it('does not run project reject-all when confirmation is cancelled', async () => {
    dialogOpenSpy.mockReturnValue({ afterClosed: () => of(false) });
    const button: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="project-suggestions-reject-all-btn"]'
    );

    button.click();
    await fixture.whenStable();

    expect(dialogOpenSpy).toHaveBeenCalled();
    expect(setPendingSpy).not.toHaveBeenCalled();
  });
});
