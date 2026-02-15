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
  let restoreRejectedSpy: ReturnType<typeof vi.fn>;
  let deleteRejectedSpy: ReturnType<typeof vi.fn>;
  let runRegressionSpy: ReturnType<typeof vi.fn>;

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
      setPendingStatusForAll: vi.fn().mockResolvedValue(3),
      restoreRejectedFromArchive: vi.fn().mockResolvedValue(2),
      deleteRejectedFromArchive: vi.fn().mockResolvedValue(2)
    };
    runRegressionSpy = suggestionsServiceMock.runRegressionReport;
    setPendingSpy = suggestionsServiceMock.setPendingStatusForAll;
    restoreRejectedSpy = suggestionsServiceMock.restoreRejectedFromArchive;
    deleteRejectedSpy = suggestionsServiceMock.deleteRejectedFromArchive;

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

  it('runs restore-all in rejected mode only after confirmation', async () => {
    fixture.componentInstance.onProjectScopeChange('rejected');
    fixture.detectChanges();
    dialogOpenSpy.mockReturnValue({ afterClosed: () => of(true) });

    const button: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="project-suggestions-restore-all-btn"]'
    );
    button.click();
    await fixture.whenStable();

    expect(dialogOpenSpy).toHaveBeenCalled();
    expect(restoreRejectedSpy).toHaveBeenCalledTimes(1);
    expect(setPendingSpy).not.toHaveBeenCalled();
  });

  it('does not run delete-all in rejected mode when confirmation is cancelled', async () => {
    fixture.componentInstance.onProjectScopeChange('rejected');
    fixture.detectChanges();
    dialogOpenSpy.mockReturnValue({ afterClosed: () => of(false) });

    const button: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="project-suggestions-delete-all-btn"]'
    );
    button.click();
    await fixture.whenStable();

    expect(dialogOpenSpy).toHaveBeenCalled();
    expect(deleteRejectedSpy).not.toHaveBeenCalled();
  });

  it('renders regression summary panel after successful run', async () => {
    const button: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="project-suggestions-run-regression-btn"]'
    );
    button.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(runRegressionSpy).toHaveBeenCalledTimes(1);
    const summary = fixture.nativeElement.querySelector('[data-testid="regression-report-summary"]');
    expect(summary).not.toBeNull();
  });

  it('scrolls to regression panel after successful run', async () => {
    const originalScrollIntoView = (HTMLElement.prototype as any).scrollIntoView;
    const scrollMock = vi.fn();
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
      configurable: true,
      writable: true,
      value: scrollMock
    });

    try {
      const button: HTMLButtonElement = fixture.nativeElement.querySelector(
        '[data-testid="project-suggestions-run-regression-btn"]'
      );
      button.click();
      await fixture.whenStable();
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 20));

      expect(scrollMock).toHaveBeenCalled();
    } finally {
      if (typeof originalScrollIntoView === 'function') {
        Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
          configurable: true,
          writable: true,
          value: originalScrollIntoView
        });
      } else {
        delete (HTMLElement.prototype as any).scrollIntoView;
      }
    }
  });

  it('scrolls shell content container to regression panel when container exists', async () => {
    const shellContainer = document.createElement('mat-tab-nav-panel');
    shellContainer.classList.add('content-inner');
    let containerScrollTop = 120;
    Object.defineProperty(shellContainer, 'scrollTop', {
      configurable: true,
      get: () => containerScrollTop,
      set: (value: number) => {
        containerScrollTop = Number(value);
      }
    });

    const scrollToMock = vi.fn((options?: ScrollToOptions | number) => {
      if (typeof options === 'number') {
        containerScrollTop = options;
        return;
      }

      if (options && typeof options.top === 'number') {
        containerScrollTop = options.top;
      }
    });
    (shellContainer as unknown as { scrollTo: typeof scrollToMock }).scrollTo = scrollToMock;
    document.body.appendChild(shellContainer);

    try {
      const button: HTMLButtonElement = fixture.nativeElement.querySelector(
        '[data-testid="project-suggestions-run-regression-btn"]'
      );
      button.click();
      await fixture.whenStable();
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 20));

      expect(scrollToMock).toHaveBeenCalled();
    } finally {
      shellContainer.remove();
    }
  });

  it('renders regression error panel when run fails', async () => {
    runRegressionSpy.mockRejectedValueOnce(new Error('Regression failed for test'));

    const button: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="project-suggestions-run-regression-btn"]'
    );
    button.click();
    await fixture.whenStable();
    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('[data-testid="regression-report-error"]');
    expect(error).not.toBeNull();
    expect((error.textContent as string).trim()).toContain('Regression failed for test');
  });
});
