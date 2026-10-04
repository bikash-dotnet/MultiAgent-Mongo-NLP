import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AnalyticsService } from './analytics.service';

describe('AnalyticsService', () => {
  let service: AnalyticsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AnalyticsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads admin metrics from the gateway', () => {
    service.metrics().subscribe((metrics) => {
      expect(metrics.totalExecutions).toBe(3);
      expect(metrics.semanticCacheHitRate).toBe(0.5);
    });

    const req = http.expectOne('/api/admin/analytics');
    expect(req.request.method).toBe('GET');
    req.flush({
      totalExecutions: 3,
      semanticCacheHits: 1,
      semanticCacheHitRate: 0.5,
      totalLlmTokens: 10,
      sensitiveAccessCount: 0,
      overrideCount: 0,
      p95DurationMs: 12,
      exportCounts: {},
      dataSourceCounts: {},
      lastExecutionAt: null
    });
  });
});
