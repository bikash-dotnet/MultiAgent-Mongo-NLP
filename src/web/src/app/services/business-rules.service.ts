import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  BusinessRuleResponse,
  BusinessRuleRequest,
  ColumnDefinitionReorderRequest,
  ColumnDefinitionRequest,
  ColumnDefinitionResponse
} from '../models/business-rules';

@Injectable({ providedIn: 'root' })
export class BusinessRulesService {
  constructor(private readonly http: HttpClient) {}

  // Business rules
  listRules(): Observable<BusinessRuleResponse[]> {
    return this.http.get<BusinessRuleResponse[]>('/api/business-rules');
  }

  getRule(id: string): Observable<BusinessRuleResponse> {
    return this.http.get<BusinessRuleResponse>(`/api/business-rules/${encodeURIComponent(id)}`);
  }

  createRule(request: BusinessRuleRequest): Observable<BusinessRuleResponse> {
    return this.http.post<BusinessRuleResponse>('/api/business-rules', request);
  }

  updateRule(id: string, request: BusinessRuleRequest, version: number): Observable<BusinessRuleResponse> {
    return this.http.put<BusinessRuleResponse>(
      `/api/business-rules/${encodeURIComponent(id)}?version=${version}`,
      request
    );
  }

  deleteRule(id: string): Observable<void> {
    return this.http.delete<void>(`/api/business-rules/${encodeURIComponent(id)}`);
  }

  // Column definitions
  listColumnDefinitions(): Observable<ColumnDefinitionResponse[]> {
    return this.http.get<ColumnDefinitionResponse[]>('/api/column-definitions');
  }

  getColumnDefinition(id: string): Observable<ColumnDefinitionResponse> {
    return this.http.get<ColumnDefinitionResponse>(`/api/column-definitions/${encodeURIComponent(id)}`);
  }

  createColumnDefinition(request: ColumnDefinitionRequest): Observable<ColumnDefinitionResponse> {
    return this.http.post<ColumnDefinitionResponse>('/api/column-definitions', request);
  }

  updateColumnDefinition(id: string, request: ColumnDefinitionRequest, version: number): Observable<ColumnDefinitionResponse> {
    return this.http.put<ColumnDefinitionResponse>(
      `/api/column-definitions/${encodeURIComponent(id)}?version=${version}`,
      request
    );
  }

  deleteColumnDefinition(id: string): Observable<void> {
    return this.http.delete<void>(`/api/column-definitions/${encodeURIComponent(id)}`);
  }

  reorderColumnDefinitions(request: ColumnDefinitionReorderRequest): Observable<void> {
    return this.http.post<void>('/api/column-definitions/reorder', request);
  }
}
