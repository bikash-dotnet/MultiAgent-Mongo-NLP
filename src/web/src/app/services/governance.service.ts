import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AccessRequest } from '../models/governance';

@Injectable({ providedIn: 'root' })
export class GovernanceService {
  constructor(private readonly http: HttpClient) {}

  list(status?: string): Observable<AccessRequest[]> {
    const params = status ? new HttpParams().set('status', status) : undefined;
    return this.http.get<AccessRequest[]>('/api/access-requests', { params });
  }

  approve(id: string, notes?: string): Observable<AccessRequest> {
    return this.http.post<AccessRequest>(`/api/access-requests/${id}/approve`, { notes: notes ?? null });
  }

  reject(id: string, notes?: string): Observable<AccessRequest> {
    return this.http.post<AccessRequest>(`/api/access-requests/${id}/reject`, { notes: notes ?? null });
  }

  override(id: string, notes: string): Observable<AccessRequest> {
    return this.http.post<AccessRequest>(`/api/access-requests/${id}/override`, { notes });
  }
}
