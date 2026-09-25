import { Injectable, inject, signal } from '@angular/core';
import { VAPID_PUBLIC_KEY } from '../core/config/api-config';
import { NotifikasiService } from './notifikasi.service';
import { PushPeramban } from './push-peramban';
import { kunciAplikasiDari } from './vapid-kunci';

/** Keadaan langganan Web Push pada perangkat ini. */
export type StatusPush =
  | 'memeriksa'
  | 'tidak-didukung'
  | 'tidak-dikonfigurasi'
  | 'tanpa-service-worker'
  | 'ditolak'
  | 'mati'
  | 'aktif';

/**
 * Langganan Web Push perangkat ini (#44, #45). Peramban dan server harus selalu sepakat: langganan
 * baru hanya bertahan bila server menerimanya, dan langganan lama hanya dilepas dari peramban setelah
 * server menghapusnya. Tanpa itu server menyimpan endpoint mati atau peramban menerima push yang
 * tidak lagi dikenal server.
 */
@Injectable({ providedIn: 'root' })
export class LanggananPush {
  private readonly peramban = inject(PushPeramban);
  private readonly notifikasi = inject(NotifikasiService);
  private readonly kunci = inject(VAPID_PUBLIC_KEY);

  private readonly _status = signal<StatusPush>('memeriksa');

  readonly status = this._status.asReadonly();

  /** Membaca keadaan sekarang tanpa mengubah apa pun. */
  async periksaAsync(): Promise<StatusPush> {
    this._status.set(await this.bacaStatusAsync());
    return this._status();
  }

  /** Meminta izin, berlangganan, lalu mendaftarkannya ke server. Galat server dilempar ke pemanggil. */
  async aktifkanAsync(): Promise<StatusPush> {
    const awal = await this.bacaStatusAsync();
    if (awal !== 'mati') {
      this._status.set(awal);
      return awal;
    }

    const izin = await this.peramban.mintaIzinAsync();
    if (izin !== 'granted') {
      this._status.set(izin === 'denied' ? 'ditolak' : 'mati');
      return this._status();
    }

    const registrasi = await this.peramban.registrasiAsync();
    if (!registrasi) {
      this._status.set('tanpa-service-worker');
      return this._status();
    }

    const langganan = await registrasi.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: kunciAplikasiDari(this.kunci),
    });
    const { endpoint, keys } = langganan.toJSON();
    try {
      if (!endpoint || !keys?.['p256dh'] || !keys['auth']) {
        throw new Error('Peramban tidak memberi kunci langganan yang lengkap.');
      }

      await this.notifikasi.langgananAsync(endpoint, keys['p256dh'], keys['auth']);
    } catch (galat) {
      // Server tidak menerima: lepaskan juga di peramban supaya keduanya tetap sepakat.
      await langganan.unsubscribe();
      throw galat;
    }

    this._status.set('aktif');
    return 'aktif';
  }

  /** Menghapus di server dulu; bila gagal, langganan di peramban dipertahankan dan galatnya dilempar. */
  async matikanAsync(): Promise<StatusPush> {
    const langganan = await this.langgananSaatIniAsync();
    if (langganan) {
      await this.notifikasi.hapusLanggananAsync(langganan.endpoint);
      await langganan.unsubscribe();
    }

    this._status.set('mati');
    return 'mati';
  }

  private async bacaStatusAsync(): Promise<StatusPush> {
    if (!this.peramban.didukung()) {
      return 'tidak-didukung';
    }

    if (this.kunci.trim().length === 0) {
      return 'tidak-dikonfigurasi';
    }

    const registrasi = await this.peramban.registrasiAsync();
    if (!registrasi) {
      return 'tanpa-service-worker';
    }

    if (await registrasi.pushManager.getSubscription()) {
      return 'aktif';
    }

    return this.peramban.izin() === 'denied' ? 'ditolak' : 'mati';
  }

  private async langgananSaatIniAsync(): Promise<PushSubscription | null> {
    const registrasi = await this.peramban.registrasiAsync();
    return registrasi ? registrasi.pushManager.getSubscription() : null;
  }
}
