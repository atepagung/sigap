// Service worker DUMMY milik shell (bukan milik remote SIGAP): menampilkan Web Push dan membuka shell saat diklik.
//
// [ASUMSI] Di platform asli, shell/platform yang mendaftarkan service worker di origin-nya; remote hanya
// berlangganan lewat pushManager (DUMMY_REGISTRY bagian 6.2 butir 103). Berkas ini hanya tiruan supaya
// alur langganan di sigap-web dapat dicoba. Bentuk muatan = satu butir GET /notifikasi (butir 94):
// { kode, tingkat, judul, pesan, terkait }.

self.addEventListener('push', (event) => {
  let muatan;
  try {
    muatan = event.data ? event.data.json() : null;
  } catch {
    muatan = null;
  }

  // Push tanpa muatan yang dapat dibaca tetap harus menampilkan sesuatu: peramban mewajibkan setiap push
  // memunculkan notifikasi (userVisibleOnly), dan peringatan kedaruratan tidak boleh jatuh diam-diam.
  const judul = muatan?.judul ?? 'Peringatan SIGAP';
  const opsi = {
    body: muatan?.pesan ?? 'Buka SIGAP untuk melihat peringatan terbaru.',
    tag: muatan ? `${muatan.kode}:${muatan.terkait?.id ?? ''}` : 'sigap',
    requireInteraction: muatan?.tingkat === 'GENTING',
  };
  event.waitUntil(self.registration.showNotification(judul, opsi));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((jendela) => {
      const ada = jendela[0];
      return ada ? ada.focus() : self.clients.openWindow('/');
    }),
  );
});
