import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { GovernanceService } from './governance.service';

describe('GovernanceService', () => {
  let service: GovernanceService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(GovernanceService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists access requests, optionally filtered by status', () => {
    service.list('PENDING_LEAD').subscribe((requests) => expect(requests.length).toBe(1));

    const req = http.expectOne((request) => request.url === '/api/access-requests' && request.params.get('status') === 'PENDING_LEAD');
    expect(req.request.method).toBe('GET');
    req.flush([{ id: 'req_1', status: 'PENDING_LEAD' }]);
  });

  it('approves, rejects, and overrides', () => {
    service.approve('req_1', 'ok').subscribe();
    http.expectOne('/api/access-requests/req_1/approve').flush({ id: 'req_1' });

    service.reject('req_1').subscribe();
    http.expectOne('/api/access-requests/req_1/reject').flush({ id: 'req_1' });

    service.override('req_1', 'urgent').subscribe();
    const override = http.expectOne('/api/access-requests/req_1/override');
    expect(override.request.body).toEqual({ notes: 'urgent' });
    override.flush({ id: 'req_1' });
  });
});
