import { HttpInterceptorFn } from '@angular/common/http';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = sessionStorage.getItem('access_token');
  const sessionId = sessionStorage.getItem('session_id');
  let headers = req.headers;
  if (token) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }
  if (sessionId) {
    headers = headers.set('X-Session-Id', sessionId);
  }
  return next(req.clone({ headers }));
};
