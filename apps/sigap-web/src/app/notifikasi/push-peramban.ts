import { Injectable } from '@angular/core';

/**
 * Satu-satunya tempat yang menyentuh API Push peramban, supaya `LanggananPush` dapat diuji dengan tiruan.
 *
 * Service worker BUKAN milik remote ini: ia harus satu origin dengan halaman, sedangkan remote dilayani
 * dari origin/path lain daripada shell. `registrasiAsync` hanya mencari yang sudah didaftarkan shell
 * dan tidak pernah memanggil `register()` (DUMMY_REGISTRY bagian 6.2 butir 103).
 */
@Injectable({ providedIn: 'root' })
export class PushPeramban {
  didukung(): boolean {
    return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
  }

  izin(): NotificationPermission {
    return Notification.permission;
  }

  mintaIzinAsync(): Promise<NotificationPermission> {
    return Notification.requestPermission();
  }

  /** `getRegistration`, bukan `ready`: `ready` menggantung selamanya bila tak ada service worker. */
  registrasiAsync(): Promise<ServiceWorkerRegistration | undefined> {
    return navigator.serviceWorker.getRegistration();
  }
}
