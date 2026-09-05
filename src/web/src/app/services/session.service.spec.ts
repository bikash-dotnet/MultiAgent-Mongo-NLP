import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { SessionService } from './session.service';

describe('SessionService', () => {
  let service: SessionService;
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });
    service = TestBed.inject(SessionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads a time-aware greeting from the gateway', () => {
    service.greeting().subscribe((greeting) => {
      expect(greeting.message).toBe('Hi Bikash, Good morning');
      expect(greeting.chips.length).toBeGreaterThan(0);
    });

    const req = http.expectOne('/api/session/greeting');
    expect(req.request.method).toBe('GET');
    req.flush({
      displayName: 'Bikash',
      period: 'morning',
      message: 'Hi Bikash, Good morning',
      chips: ['Listings in Los Angeles']
    });
  });
});
