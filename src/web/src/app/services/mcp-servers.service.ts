import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  McpServerDefinition,
  McpServerDetailResponse,
  McpServerListResponse,
  McpServerRequest,
  McpServerSummary,
  McpTestResult,
  McpToolDescriptor
} from '../models/mcp-servers';

@Injectable({ providedIn: 'root' })
export class McpServersService {
  constructor(private readonly http: HttpClient) {}

  listServers(): Observable<McpServerListResponse> {
    return this.http.get<McpServerListResponse>('/api/mcp-servers');
  }

  getServer(id: string): Observable<McpServerDetailResponse> {
    return this.http.get<McpServerDetailResponse>(
      `/api/mcp-servers/${encodeURIComponent(id)}`
    );
  }

  createServer(request: McpServerRequest): Observable<McpServerDefinition> {
    return this.http.post<McpServerDefinition>('/api/mcp-servers', request);
  }

  updateServer(
    id: string,
    request: McpServerRequest,
    version: number
  ): Observable<McpServerDefinition> {
    return this.http.put<McpServerDefinition>(
      `/api/mcp-servers/${encodeURIComponent(id)}?version=${version}`,
      request
    );
  }

  deleteServer(id: string): Observable<void> {
    return this.http.delete<void>(`/api/mcp-servers/${encodeURIComponent(id)}`);
  }

  enableServer(id: string): Observable<McpServerDefinition> {
    return this.http.post<McpServerDefinition>(
      `/api/mcp-servers/${encodeURIComponent(id)}/enable`,
      {}
    );
  }

  disableServer(id: string): Observable<McpServerDefinition> {
    return this.http.post<McpServerDefinition>(
      `/api/mcp-servers/${encodeURIComponent(id)}/disable`,
      {}
    );
  }

  testServer(id: string): Observable<McpTestResult> {
    return this.http.post<McpTestResult>(
      `/api/mcp-servers/${encodeURIComponent(id)}/test`,
      {}
    );
  }

  listTools(id: string): Observable<McpToolDescriptor[]> {
    return this.http.get<McpToolDescriptor[]>(
      `/api/mcp-servers/${encodeURIComponent(id)}/tools`
    );
  }
}
