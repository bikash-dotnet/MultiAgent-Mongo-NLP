import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AdminAnalyticsComponent } from './admin-analytics.component';
import { AnalyticsService } from '../services/analytics.service';
import { AdminAnalyticsMetrics } from '../models/analytics';

describe('AdminAnalyticsComponent', () => {
  const metrics: AdminAnalyticsMetrics = {
    totalExecutions: 12,
    semanticCacheHits: 3,
    semanticCacheHitRate: 0.25,
    totalLlmTokens: 420,
    sensitiveAccessCount: 2,
    overrideCount: 1,
    p95DurationMs: 184,
    exportCounts: { CSV: 5, XLSX: 2 },
    dataSourceCounts: { Mongo: 10, EnterpriseCoreREST: 2 },
    lastExecutionAt: '2026-09-30T12:00:00Z'
  };

  function configure(value: AdminAnalyticsMetrics) {
    TestBed.configureTestingModule({
      imports: [AdminAnalyticsComponent],
      providers: [{ provide: AnalyticsService, useValue: { metrics: () => of(value) } }]
    });
  }

  it('renders the aggregate metrics', () => {
    configure(metrics);
    const fixture = TestBed.createComponent(AdminAnalyticsComponent);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('12');
    expect(text).toContain('25%');
    expect(text).toContain('420');
    expect(text).toContain('CSV: 5');
    expect(text).toContain('Mongo: 10');
  });

  it('tolerates an empty audit log', () => {
    configure({ ...metrics, totalExecutions: 0, exportCounts: {}, dataSourceCounts: {} });
    const fixture = TestBed.createComponent(AdminAnalyticsComponent);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('No exports yet.');
    expect(text).toContain('No executions yet.');
  });
});
