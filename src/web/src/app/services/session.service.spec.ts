import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { SessionService } from './session.service';

describe('SessionService', () => {
  let service: SessionService;
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
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

  it('reads the role from the stored token', () => {
    const payload = btoa(JSON.stringify({ role: 'Data Owner / Admin' }));
    sessionStorage.setItem('access_token', `header.${payload}.signature`);

    expect(service.role()).toBe('Data Owner / Admin');
  });

  it('reads the role from a base64url encoded token', () => {
    const payload = btoa(JSON.stringify({ role: 'Data Owner / Admin', nonce: '>>>' }))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');
    sessionStorage.setItem('access_token', `header.${payload}.signature`);

    expect(service.role()).toBe('Data Owner / Admin');
  });
});
