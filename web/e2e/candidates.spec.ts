import { test, expect, Route } from '@playwright/test';

const apiBase = 'http://localhost:5181';

const fakeCandidates = [
  {
    id: '00000000-0000-0000-0000-000000000001',
    fullName: 'Ada Lovelace',
    email: 'ada@example.com',
    location: 'London, UK',
    reviewStatus: 'Pending',
    aiSeniorityLevel: 'Senior',
    aiYearsExperienceEstimate: 8,
    topSkill: 'PyTorch',
    aiSummary: 'Senior ML engineer with deep production experience.',
    skills: ['PyTorch', 'Python', 'AWS'],
    updatedAt: '2026-05-23T10:00:00Z',
  },
  {
    id: '00000000-0000-0000-0000-000000000002',
    fullName: 'Grace Hopper',
    email: 'grace@example.com',
    location: 'New York, US',
    reviewStatus: 'Pending',
    aiSeniorityLevel: 'Staff',
    aiYearsExperienceEstimate: 11,
    topSkill: 'Python',
    aiSummary: 'Staff engineer with research + platform background.',
    skills: ['Python', 'Kubernetes'],
    updatedAt: '2026-05-22T10:00:00Z',
  },
];

const fakeDetail = (id: string) => ({
  id,
  fullName: id.endsWith('1') ? 'Ada Lovelace' : 'Grace Hopper',
  email: id.endsWith('1') ? 'ada@example.com' : 'grace@example.com',
  phone: '+1 555-123',
  location: 'London, UK',
  linkedInUrl: 'https://linkedin.com/in/ada',
  gitHubUrl: 'https://github.com/ada',
  summary: 'Resume summary.',
  sourceFileName: 'ada.docx',
  reviewStatus: 'Pending',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-05-01T00:00:00Z',
  lastEditedBy: 'system',
  skills: [
    { id: 's1', name: 'PyTorch', orderIndex: 0 },
    { id: 's2', name: 'Python', orderIndex: 1 },
  ],
  workExperiences: [
    {
      id: 'w1', title: 'Senior ML Engineer', company: 'Acme',
      startDate: '2020-01-01', endDate: null,
      description: 'Led model serving platform.', orderIndex: 0,
    },
  ],
  education: [{ id: 'e1', institution: 'MIT', degree: 'MSc', field: 'CS', graduationYear: 2018, orderIndex: 0 }],
  certifications: [],
  projects: [],
  aiFields: {
    aiSummary: 'Senior ML engineer with deep production experience.',
    aiSeniorityLevel: 'Senior',
    aiSeniorityRationale: '8 years across multiple roles.',
    aiTopStrengths: '["PyTorch","Distributed systems","Mentoring"]',
    aiSkillCategories: '{"Languages":["Python"],"Frameworks":["PyTorch"]}',
    aiYearsExperienceEstimate: 8,
    aiSuggestedRoles: '["Staff ML Engineer"]',
    aiInterviewFocusAreas: '["System design","LLM evaluation"]',
    lastEnrichedAt: '2026-05-01T00:00:00Z',
    enrichmentStatus: 'Completed',
    lastError: null,
  },
  aiOverrides: [],
});

async function stubApi(page: import('@playwright/test').Page) {
  await page.route(/.*\/api\/.*/, async (route: Route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    const method = route.request().method();

    if (path === '/api/candidates' && method === 'GET') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(fakeCandidates) });
    }
    if (path === '/api/report' && method === 'GET') {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([fakeDetail(fakeCandidates[0].id)]) });
    }
    if (/\/api\/candidates\/[0-9a-f-]+\/review$/.test(path) && method === 'POST') {
      const body = route.request().postDataJSON();
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ id: 'x', status: body.status }) });
    }
    if (/\/api\/candidates\/[0-9a-f-]+\/ai-fields\/.*\/regenerate$/.test(path) && method === 'POST') {
      return route.fulfill({
        status: 200, contentType: 'application/json',
        body: JSON.stringify({ newValue: 'Regenerated value!', historyId: 'h1' }),
      });
    }
    if (/\/api\/candidates\/[0-9a-f-]+$/.test(path) && method === 'GET') {
      const id = path.split('/').pop()!;
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(fakeDetail(id)) });
    }
    // Default catch-all
    await route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
  });
}

test('candidates list shows view toggle, filtering, and reviewed count', async ({ page }) => {
  await stubApi(page);
  await page.goto('/candidates');

  await expect(page.getByText('Ada Lovelace')).toBeVisible();
  await expect(page.getByText('Grace Hopper')).toBeVisible();

  // Table view exists
  await expect(page.locator('[data-testid="table-view"]')).toBeVisible();

  // Filter narrows results
  await page.locator('[data-testid="search-input"]').fill('Grace');
  await expect(page.getByText('Ada Lovelace')).not.toBeVisible();
  await expect(page.getByText('Grace Hopper')).toBeVisible();
  await page.locator('[data-testid="search-input"]').fill('');

  // Toggle to card view
  await page.locator('[data-testid="view-toggle"] button').nth(1).click();
  await expect(page.locator('[data-testid="card-view"]')).toBeVisible();
});

test('candidate detail renders AI fields with badge + regenerate; mark as reviewed updates header', async ({ page }) => {
  await stubApi(page);
  await page.goto('/candidates');
  await page.getByText('Ada Lovelace').click();

  await expect(page.locator('[data-testid="candidate-name"]')).toHaveText('Ada Lovelace');
  await expect(page.locator('[data-testid="ai-fields"]')).toBeVisible();

  // AI summary badge defaults to AI-generated
  await expect(page.locator('[data-testid="ai-badge-AiSummary"]')).toHaveText('AI-generated');

  // Regenerate opens modal, after submit badge becomes Regenerated
  await page.locator('[data-testid="regen-AiSummary"]').click();
  await page.locator('[data-testid="regen-submit"]').click();
  await expect(page.locator('[data-testid="ai-badge-AiSummary"]')).toHaveText('Regenerated');

  // Mark as reviewed flips status
  await page.locator('[data-testid="mark-reviewed"]').click();
  await expect(page.locator('[data-testid="review-status"]')).toHaveText('Reviewed');
  await expect(page.locator('[data-testid="reviewed-count"]')).toHaveText('1');
});

test('report page renders all 7 visualization sections', async ({ page }) => {
  await stubApi(page);
  await page.goto('/report');
  for (const v of [
    'skills-heatmap',
    'experience-timeline',
    'seniority-distribution',
    'tech-stack',
    'side-by-side',
    'education-breakdown',
    'leaderboard',
  ]) {
    await expect(page.locator(`[data-viz="${v}"]`)).toBeVisible();
  }
});
