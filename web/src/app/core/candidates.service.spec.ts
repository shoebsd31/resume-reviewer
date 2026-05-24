import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { CandidatesService } from './candidates.service';
import { CandidateSummary } from './models';

describe('CandidatesService', () => {
  let service: CandidatesService;
  let http: { get: jest.Mock; post: jest.Mock };

  beforeEach(() => {
    http = { get: jest.fn(), post: jest.fn() };
    TestBed.configureTestingModule({
      providers: [
        CandidatesService,
        { provide: HttpClient, useValue: http },
      ],
    });
    service = TestBed.inject(CandidatesService);
  });

  it('loads all candidates and exposes reviewed count', async () => {
    const rows: CandidateSummary[] = [
      makeRow('1', 'Reviewed'),
      makeRow('2', 'Pending'),
      makeRow('3', 'Reviewed'),
    ];
    http.get.mockReturnValue(of(rows));

    await service.loadAll();

    expect(service.candidates()).toHaveLength(3);
    expect(service.reviewedCount()).toBe(2);
  });

  it('updates local state when review status is set', async () => {
    const rows: CandidateSummary[] = [makeRow('1', 'Pending')];
    http.get.mockReturnValue(of(rows));
    http.post.mockReturnValue(of({ id: '1', status: 'Reviewed' }));

    await service.loadAll();
    await service.setReviewStatus('1', 'Reviewed');

    expect(service.candidates()[0].reviewStatus).toBe('Reviewed');
    expect(service.reviewedCount()).toBe(1);
  });

  function makeRow(id: string, status: 'Reviewed' | 'Pending' | 'Rejected'): CandidateSummary {
    return {
      id, fullName: 'Name ' + id, email: id + '@e.com',
      location: null, reviewStatus: status,
      aiSeniorityLevel: null, aiYearsExperienceEstimate: null,
      topSkill: null, aiSummary: null, skills: [],
      updatedAt: new Date().toISOString(),
    };
  }
});
