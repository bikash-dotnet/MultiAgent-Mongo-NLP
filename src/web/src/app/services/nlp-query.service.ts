import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { NlpQueryResponse } from '../models/nlp-query-response';

@Injectable({ providedIn: 'root' })
export class NlpQueryService {
  constructor(private readonly http: HttpClient) {}

  query(utterance: string): Observable<NlpQueryResponse> {
    return this.http.post<NlpQueryResponse>('/api/nlp/query', { utterance });
  }
}
