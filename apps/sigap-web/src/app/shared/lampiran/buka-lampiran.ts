import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

/**
 * Membuka lampiran di tab baru. `LampiranDto.url` (#11) butuh header `Authorization` — tautan
 * `<a href>` biasa tidak melewati interceptor, jadi berkas diambil lewat `HttpClient` (Bearer ikut
 * terpasang) lalu dibuka sebagai object URL sementara.
 */
export async function bukaLampiranAsync(http: HttpClient, url: string): Promise<void> {
  const blob = await firstValueFrom(http.get(url, { responseType: 'blob' }));
  const objectUrl = URL.createObjectURL(blob);
  window.open(objectUrl, '_blank', 'noopener');
  setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
}
