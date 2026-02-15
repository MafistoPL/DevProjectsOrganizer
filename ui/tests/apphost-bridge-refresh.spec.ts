import { expect, test, type Page } from '@playwright/test';

type WebviewRoot = {
  id: string;
  path: string;
  status: string;
  projectCount: number;
  ongoingSuggestionCount: number;
  lastScanState: string | null;
  lastScanAt: string | null;
  lastScanFiles: number | null;
};

type WebviewSuggestion = {
  id: string;
  scanSessionId: string;
  rootPath: string;
  name: string;
  score: number;
  kind: string;
  path: string;
  reason: string;
  extensionsSummary: string;
  markers: string[];
  techHints: string[];
  createdAt: string;
  status: 'Pending' | 'Accepted' | 'Rejected';
};

const ROOTS_FIXTURE: WebviewRoot[] = [
  {
    id: 'root-1',
    path: 'D:\\tests',
    status: 'scanned',
    projectCount: 4,
    ongoingSuggestionCount: 1,
    lastScanState: 'Completed',
    lastScanAt: '2026-02-12T21:00:00.000Z',
    lastScanFiles: 842
  }
];

const SUGGESTIONS_FIXTURE: WebviewSuggestion[] = [
  {
    id: 's-pending-1',
    scanSessionId: 'scan-1',
    rootPath: 'D:\\tests',
    name: 'alpha-api',
    score: 0.91,
    kind: 'ProjectRoot',
    path: 'D:\\tests\\alpha-api',
    reason: 'markers: .sln',
    extensionsSummary: 'cs=24, json=3',
    markers: ['.sln'],
    techHints: ['csharp'],
    createdAt: '2026-02-12T21:00:10.000Z',
    status: 'Pending'
  },
  {
    id: 's-archive-1',
    scanSessionId: 'scan-1',
    rootPath: 'D:\\tests',
    name: 'beta-cli',
    score: 0.61,
    kind: 'Collection',
    path: 'D:\\tests\\beta-cli',
    reason: 'ext histogram',
    extensionsSummary: 'cpp=10, h=8',
    markers: ['Makefile'],
    techHints: ['cpp'],
    createdAt: '2026-02-12T21:00:09.000Z',
    status: 'Rejected'
  },
  {
    id: 's-archive-2',
    scanSessionId: 'scan-1',
    rootPath: 'D:\\tests',
    name: 'gamma-web',
    score: 0.66,
    kind: 'Collection',
    path: 'D:\\tests\\gamma-web',
    reason: 'package.json',
    extensionsSummary: 'ts=12, html=4',
    markers: ['package.json'],
    techHints: ['typescript'],
    createdAt: '2026-02-12T21:00:08.000Z',
    status: 'Accepted'
  }
];

const installWebviewBridgeMock = async (
  page: Page,
  roots: WebviewRoot[],
  suggestions: WebviewSuggestion[]
) => {
  await page.addInitScript(
    ({ seededRoots, seededSuggestions }) => {
      const listeners: Array<(event: { data: unknown }) => void> = [];
      let rootsState = [...seededRoots];
      let suggestionsState = [...seededSuggestions];
      const scansState: Array<unknown> = [];

      const emit = (payload: unknown) => {
        for (const listener of listeners) {
          listener({ data: payload });
        }
      };

      const sendResponse = (id: string, type: string, data: unknown) => {
        emit({ id, type, ok: true, data });
      };

      const sendError = (id: string, type: string, error: string) => {
        emit({ id, type, ok: false, error });
      };

      (window as any).chrome = {
        webview: {
          addEventListener: (type: string, callback: (event: { data: unknown }) => void) => {
            if (type === 'message') {
              listeners.push(callback);
            }
          },
          postMessage: (request: { id: string; type: string; payload?: any }) => {
            setTimeout(() => {
              const { id, type, payload } = request;
              switch (type) {
                case 'roots.list':
                  sendResponse(id, type, rootsState);
                  return;
                case 'scan.list':
                  sendResponse(id, type, scansState);
                  return;
                case 'suggestions.list':
                  sendResponse(id, type, suggestionsState);
                  return;
                case 'suggestions.delete': {
                  const suggestionId = payload?.id;
                  const before = suggestionsState.length;
                  suggestionsState = suggestionsState.filter((item) => item.id !== suggestionId);
                  sendResponse(id, type, { id: suggestionId, deleted: suggestionsState.length < before });
                  return;
                }
                default:
                  sendError(id, type, `Unknown mock request: ${type}`);
              }
            }, 0);
          }
        }
      };
    },
    { seededRoots: roots, seededSuggestions: suggestions }
  );
};

test('scan renders host-backed cards without requiring resize/toggle', async ({ page }) => {
  await installWebviewBridgeMock(page, ROOTS_FIXTURE, SUGGESTIONS_FIXTURE);
  await page.goto('/scan');

  await expect(page.getByTestId('scan-root-path').first()).toHaveText('D:\\tests');
  await expect(
    page.getByTestId('project-suggest-list').locator('[data-testid="project-name"]').first()
  ).toHaveText('alpha-api');
});

test('rejected delete updates UI immediately in host-backed mode', async ({ page }) => {
  await installWebviewBridgeMock(page, ROOTS_FIXTURE, SUGGESTIONS_FIXTURE);
  await page.goto('/suggestions');

  await page.getByTestId('project-suggest-scope').getByText('Rejected').click();
  await page.getByTestId('project-suggest-layout').getByText('Grid').click();

  const cards = page.getByTestId('project-suggest-list').locator('.suggestion-card');
  await expect(cards).toHaveCount(1);

  await cards.first().getByTestId('project-suggest-delete-btn').click();
  await expect(cards).toHaveCount(0);
});
