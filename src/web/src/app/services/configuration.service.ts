import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ConfigSectionResponse,
  ConfigSchemaResponse,
  ConfigResetRequest,
  UpdateConfigSectionRequest
} from '../models/configuration';

@Injectable({ providedIn: 'root' })
export class ConfigurationService {
  constructor(private readonly http: HttpClient) {}

  list(): Observable<ConfigSectionResponse[]> {
    return this.http.get<ConfigSectionResponse[]>('/api/config');
  }

  get(section: string): Observable<ConfigSectionResponse> {
    return this.http.get<ConfigSectionResponse>(`/api/config/${encodeURIComponent(section)}`);
  }

  schema(): Observable<ConfigSchemaResponse> {
    return this.http.get<ConfigSchemaResponse>('/api/config/schema');
  }

  update(section: string, request: UpdateConfigSectionRequest): Observable<ConfigSectionResponse> {
    return this.http.put<ConfigSectionResponse>(`/api/config/${encodeURIComponent(section)}`, request);
  }

  remove(section: string): Observable<void> {
    return this.http.delete<void>(`/api/config/${encodeURIComponent(section)}`);
  }

  reset(request: ConfigResetRequest): Observable<void> {
    return this.http.post<void>('/api/config/reset', request);
  }
}
