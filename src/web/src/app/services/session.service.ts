import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { Greeting } from '../models/greeting';

const TOKEN_KEY = 'access_token';
const SESSION_KEY = 'session_id';

@Injectable({ providedIn: 'root' })
export class SessionService {
  constructor(private readonly http: HttpClient) {}

  sessionId(): string {
    let id = sessionStorage.getItem(SESSION_KEY);
    if (!id) {
      id = `sess_${crypto.randomUUID()}`;
      sessionStorage.setItem(SESSION_KEY, id);
    }
    return id;
  }

  token(): string | null {
    return sessionStorage.getItem(TOKEN_KEY);
  }

  role(): string | null {
    const token = this.token();
    if (!token) {
      return null;
    }

    const payload = token.split('.')[1];
    if (!payload) {
      return null;
    }

    try {
      const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
      const padded = normalized.padEnd(normalized.length + ((4 - (normalized.length % 4)) % 4), '=');
      const decoded = JSON.parse(atob(padded));
      return typeof decoded.role === 'string' ? decoded.role : null;
    } catch {
      return null;
    }
  }

  bootstrap(): Observable<{ token: string }> {
    return this.http.get<{ token: string }>('/dev/token').pipe(
      tap((payload) => sessionStorage.setItem(TOKEN_KEY, payload.token))
    );
  }

  greeting(): Observable<Greeting> {
    return this.http.get<Greeting>('/api/session/greeting');
  }
}
