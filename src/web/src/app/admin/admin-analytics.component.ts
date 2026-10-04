import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { catchError, interval, of, startWith, Subscription, switchMap } from 'rxjs';
import { AdminAnalyticsMetrics } from '../models/analytics';
import { AnalyticsService } from '../services/analytics.service';

@Component({
  selector: 'app-admin-analytics',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './admin-analytics.component.html',
  styleUrl: './admin-analytics.component.scss'
})
export class AdminAnalyticsComponent implements OnInit, OnDestroy {
  metrics: AdminAnalyticsMetrics | null = null;
  error: string | null = null;
  refreshedAt: Date | null = null;
  readonly refreshIntervalMs = 10000;

  private sub = new Subscription();

  constructor(private readonly analytics: AnalyticsService) {}

  ngOnInit(): void {
    this.sub.add(
      interval(this.refreshIntervalMs)
        .pipe(
          startWith(0),
          switchMap(() =>
            this.analytics.metrics().pipe(
              catchError(() => {
                this.error = 'Unable to load analytics.';
                return of(null);
              })
            )
          )
        )
        .subscribe({
          next: (metrics) => {
            if (metrics) {
              this.metrics = metrics;
              this.error = null;
              this.refreshedAt = new Date();
            }
          }
        })
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  get cacheHitRatePercent(): number {
    return Math.round((this.metrics?.semanticCacheHitRate ?? 0) * 100);
  }

  exportEntries(): { name: string; count: number }[] {
    return this.toEntries(this.metrics?.exportCounts);
  }

  dataSourceEntries(): { name: string; count: number }[] {
    return this.toEntries(this.metrics?.dataSourceCounts);
  }

  private toEntries(counts: Record<string, number> | undefined): { name: string; count: number }[] {
    const map = counts ?? {};
    return Object.keys(map)
      .sort()
      .map((name) => ({ name, count: map[name] }));
  }
}
