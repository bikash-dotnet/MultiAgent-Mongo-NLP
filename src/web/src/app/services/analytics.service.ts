import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AdminAnalyticsMetrics } from '../models/analytics';

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  constructor(private readonly http: HttpClient) {}

  metrics(): Observable<AdminAnalyticsMetrics> {
    return this.http.get<AdminAnalyticsMetrics>('/api/admin/analytics');
  }
}
