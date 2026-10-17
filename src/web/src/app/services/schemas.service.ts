import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  SchemaDefinition,
  SchemaFieldRequest,
  SchemaListResponse,
  SchemaResponse,
  CreateSchemaRequest,
  UpdateSchemaRequest,
  ImportSchemaRequest
} from '../models/schemas';

@Injectable({ providedIn: 'root' })
export class SchemasService {
  constructor(private readonly http: HttpClient) {}

  list(): Observable<SchemaListResponse> {
    return this.http.get<SchemaListResponse>('/api/schemas');
  }

  get(id: string): Observable<SchemaResponse> {
    return this.http.get<SchemaResponse>(`/api/schemas/${id}`);
  }

  create(request: CreateSchemaRequest): Observable<SchemaResponse> {
    return this.http.post<SchemaResponse>('/api/schemas', request);
  }

  update(id: string, request: UpdateSchemaRequest): Observable<SchemaResponse> {
    return this.http.put<SchemaResponse>(`/api/schemas/${id}`, request);
  }

  addField(id: string, request: SchemaFieldRequest): Observable<SchemaResponse> {
    return this.http.post<SchemaResponse>(`/api/schemas/${id}/fields`, request);
  }

  updateField(id: string, path: string, request: SchemaFieldRequest): Observable<SchemaResponse> {
    return this.http.put<SchemaResponse>(`/api/schemas/${id}/fields/${encodeURIComponent(path)}`, request);
  }

  deleteField(id: string, path: string): Observable<SchemaResponse> {
    return this.http.delete<SchemaResponse>(`/api/schemas/${id}/fields/${encodeURIComponent(path)}`);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/schemas/${id}`);
  }

  activate(id: string): Observable<{ id: string; isActiveSource: boolean }> {
    return this.http.post<{ id: string; isActiveSource: boolean }>(`/api/schemas/${id}/activate`, {});
  }

  import(request: ImportSchemaRequest): Observable<SchemaResponse> {
    return this.http.post<SchemaResponse>('/api/schemas/import', request);
  }
}
