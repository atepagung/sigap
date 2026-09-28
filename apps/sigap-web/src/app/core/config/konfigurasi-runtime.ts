/**
 * Konfigurasi yang berbeda per lingkungan (alamat API, issuer SSO, kunci publik VAPID), dibaca dari
 * `config.json` di samping bundel saat aplikasi mulai — bukan dibakar ke bundel saat build, sehingga satu image
 * dipakai di semua lingkungan. Container menulis berkas itu dari environment variable
 * (`docker/40-sigap-config.sh`, `apps/sigap-web/.env.example`).
 *
 * [ASUMSI] Mekanisme sementara sampai shell/starter.mfe menyediakan cara resmi menyerahkan konfigurasi ke remote
 * (Lampiran E #12; DUMMY_REGISTRY bagian 9 butir 24). Hanya nilai publik: yang rahasia tidak pernah sampai ke
 * peramban.
 */
export interface KonfigurasiRuntime {
  readonly apiBaseUrl?: string;
  readonly ssoIssuerUrl?: string;
  readonly ssoClientId?: string;
  readonly vapidPublicKey?: string;
}

const KUNCI: readonly (keyof KonfigurasiRuntime)[] = [
  'apiBaseUrl',
  'ssoIssuerUrl',
  'ssoClientId',
  'vapidPublicKey',
];

let isi: KonfigurasiRuntime = {};

/** Nilai yang sudah dimuat; kosong sebelum {@link muatKonfigurasiRuntime} selesai atau tanpa `config.json`. */
export function konfigurasiRuntime(): KonfigurasiRuntime {
  return isi;
}

/**
 * Dipanggil sekali sebagai app initializer, sebelum layanan apa pun membaca token alamat. Relatif terhadap
 * modul ini (bukan dokumen), sebab di dalam shell dokumennya milik shell sedangkan bundel remote tersaji dari
 * origin lain.
 *
 * - 404 → pengembangan lokal (`ng serve` tidak punya `config.json`): nilai bawaan token dipakai.
 * - Selain itu gagal → dilempar, aplikasi tidak mulai. Lebih baik gagal terlihat daripada diam-diam
 *   memanggil alamat bawaan pengembangan dari lingkungan sungguhan.
 * - String kosong berarti tidak diisi (envsubst mengganti variabel yang tak disetel dengan "").
 */
export async function muatKonfigurasiRuntime(
  alamat: URL = new URL('config.json', import.meta.url),
  ambil: typeof fetch = fetch,
): Promise<void> {
  isi = {};
  const respons = await ambil(alamat, { cache: 'no-store' });
  if (respons.status === 404) return;
  if (!respons.ok) {
    throw new Error(`Konfigurasi runtime tidak dapat dibaca (status ${respons.status}).`);
  }

  const data: unknown = await respons.json();
  if (typeof data !== 'object' || data === null || Array.isArray(data)) {
    throw new Error('Konfigurasi runtime harus berupa objek JSON.');
  }

  const hasil: Record<string, string> = {};
  for (const kunci of KUNCI) {
    const nilai = (data as Record<string, unknown>)[kunci];
    if (nilai === undefined || nilai === null || nilai === '') continue;
    if (typeof nilai !== 'string') {
      throw new Error(`Konfigurasi runtime: "${kunci}" harus berupa teks.`);
    }
    hasil[kunci] = nilai;
  }
  isi = hasil;
}
