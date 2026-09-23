import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApprovalFlag, ConversationAnswer, ConversationTurn } from '../models/conversation';

@Injectable({ providedIn: 'root' })
export class ConversationService {
  constructor(private readonly http: HttpClient) {}

  start(utterance: string): Observable<ConversationTurn> {
    return this.http.post<ConversationTurn>('/api/conversations', { utterance });
  }

  answer(conversationId: string, answer: ConversationAnswer): Observable<ConversationTurn> {
    return this.http.post<ConversationTurn>(`/api/conversations/${conversationId}/answers`, answer);
  }

  get(conversationId: string): Observable<ConversationTurn> {
    return this.http.get<ConversationTurn>(`/api/conversations/${conversationId}`);
  }

  downloadReport(conversationId: string): Observable<Blob> {
    return this.http.get(`/api/conversations/${conversationId}/report.csv`, { responseType: 'blob' });
  }

  getApproval(): Observable<ApprovalFlag> {
    return this.http.get<ApprovalFlag>('/api/governance/approval');
  }

  setApproval(enabled: boolean): Observable<ApprovalFlag> {
    return this.http.put<ApprovalFlag>('/api/governance/approval', { enabled });
  }
}
