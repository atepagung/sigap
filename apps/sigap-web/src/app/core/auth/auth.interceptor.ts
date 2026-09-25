import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL } from '../config/api-config';
import { TokenProvider } from './token-provider';

/**
 * Menempelkan `Authorization: Bearer <token>` hanya pada permintaan ke sigap-api — bukan ke
 * Keycloak (yang login sendiri lewat body form) maupun domain lain. Header dibuat oleh library
 * HTTP standar (`HttpRequest.clone`), bukan penyusunan header manual (AGENTS.md bagian 5).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const apiBaseUrl = inject(API_BASE_URL);
  const token = inject(TokenProvider).token();

  if (!token || !req.url.startsWith(apiBaseUrl)) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
