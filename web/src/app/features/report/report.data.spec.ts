import { computeReportData } from './report.data';
import { CandidateDetail } from '../../core/models';

function fakeCandidate(overrides: Partial<CandidateDetail> = {}): CandidateDetail {
  return {
    id: overrides.id ?? 'c1',
    fullName: overrides.fullName ?? 'Ada Lovelace',
    email: 'ada@example.com',
    phone: null,
    location: null,
    linkedInUrl: null,
    gitHubUrl: null,
    summary: '',
    sourceFileName: 'ada.docx',
    reviewStatus: 'Reviewed',
    createdAt: new Date('2025-01-01').toISOString(),
    updatedAt: new Date('2025-01-02').toISOString(),
    lastEditedBy: 'system',
    skills: (overrides.skills ?? [
      { id: 's1', name: 'Python', orderIndex: 0 },
      { id: 's2', name: 'PyTorch', orderIndex: 1 },
    ]),
    workExperiences: overrides.workExperiences ?? [{
      id: 'w1', title: 'ML Eng', company: 'Acme',
      startDate: '2022-01-01', endDate: null,
      description: '', orderIndex: 0,
    }],
    education: overrides.education ?? [{
      id: 'e1', institution: 'MIT', degree: 'MSc', field: 'CS',
      graduationYear: 2020, orderIndex: 0
    }],
    certifications: [],
    projects: [],
    aiFields: overrides.aiFields ?? {
      aiSummary: 'Summary',
      aiSeniorityLevel: 'Senior',
      aiSeniorityRationale: null,
      aiTopStrengths: null,
      aiSkillCategories: null,
      aiYearsExperienceEstimate: 6,
      aiSuggestedRoles: null,
      aiInterviewFocusAreas: null,
      lastEnrichedAt: null,
      enrichmentStatus: 'Completed',
      lastError: null,
    },
    aiOverrides: [],
    ...overrides,
  };
}

describe('computeReportData', () => {
  it('computes seniority distribution', () => {
    const data = computeReportData([
      fakeCandidate({ id: '1', aiFields: { ...fakeCandidate().aiFields!, aiSeniorityLevel: 'Senior' } }),
      fakeCandidate({ id: '2', aiFields: { ...fakeCandidate().aiFields!, aiSeniorityLevel: 'Senior' } }),
      fakeCandidate({ id: '3', aiFields: { ...fakeCandidate().aiFields!, aiSeniorityLevel: 'Staff' } }),
    ]);
    const senior = data.seniorityDistribution.find((r) => r.name === 'Senior');
    const staff = data.seniorityDistribution.find((r) => r.name === 'Staff');
    expect(senior?.value).toBe(2);
    expect(staff?.value).toBe(1);
  });

  it('builds a skills heatmap with one cell per candidate × skill', () => {
    const data = computeReportData([
      fakeCandidate({
        id: '1', fullName: 'A',
        skills: [{ id: 'x', name: 'Python', orderIndex: 0 }]
      }),
      fakeCandidate({
        id: '2', fullName: 'B',
        skills: [{ id: 'y', name: 'PyTorch', orderIndex: 0 }]
      }),
    ]);
    expect(data.skillsHeatmap).toHaveLength(2);
    const pythonRowA = data.skillsHeatmap[0].series.find((s) => s.name === 'Python');
    const pythonRowB = data.skillsHeatmap[1].series.find((s) => s.name === 'Python');
    expect(pythonRowA?.value).toBe(1);
    expect(pythonRowB?.value).toBe(0);
  });

  it('produces a non-empty leaderboard sorted by score desc', () => {
    const data = computeReportData([
      fakeCandidate({
        id: '1', fullName: 'Senior',
        aiFields: { ...fakeCandidate().aiFields!, aiSeniorityLevel: 'Senior', aiYearsExperienceEstimate: 5 }
      }),
      fakeCandidate({
        id: '2', fullName: 'Principal',
        aiFields: { ...fakeCandidate().aiFields!, aiSeniorityLevel: 'Principal', aiYearsExperienceEstimate: 15 }
      }),
    ]);
    expect(data.leaderboard[0].name).toBe('Principal');
    expect(data.leaderboard[0].score).toBeGreaterThan(data.leaderboard[1].score);
    expect(data.leaderboard[0].components.length).toBeGreaterThan(0);
  });

  it('includes a tech-stack cloud aggregated by frequency', () => {
    const data = computeReportData([
      fakeCandidate({ skills: [{ id: 'a', name: 'Python', orderIndex: 0 }, { id: 'b', name: 'AWS', orderIndex: 1 }] }),
      fakeCandidate({ id: '2', skills: [{ id: 'c', name: 'Python', orderIndex: 0 }] }),
    ]);
    const python = data.techCloud.find((t) => t.name === 'Python');
    const aws = data.techCloud.find((t) => t.name === 'AWS');
    expect(python?.value).toBe(2);
    expect(aws?.value).toBe(1);
  });
});
