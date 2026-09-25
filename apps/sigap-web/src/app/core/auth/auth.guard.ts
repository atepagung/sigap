import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { TokenProvider } from './token-provider';

/** Mengalihkan ke halaman masuk bila belum ada token. Keamanan sesungguhnya tetap di API. */
export const authGuard: CanActivateFn = () => {
  if (inject(TokenProvider).sudahMasuk) {
    return true;
  }

  return inject(Router).createUrlTree(['masuk']);
};
