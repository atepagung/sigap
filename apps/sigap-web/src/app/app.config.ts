import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './core/auth/auth.interceptor';
import { muatKonfigurasiRuntime } from './core/config/konfigurasi-runtime';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Sebelum layanan mana pun membaca token alamat (API_BASE_URL dll.).
    provideAppInitializer(() => muatKonfigurasiRuntime()),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
  ],
};
