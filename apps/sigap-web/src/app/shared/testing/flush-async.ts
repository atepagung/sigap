/**
 * Mengalirkan beberapa putaran microtask. `fixture.whenStable()` hanya menunggu pekerjaan yang
 * terdaftar di `PendingTasks` Angular (mis. `HttpClient` sungguhan) — rantai `await` di `ngOnInit`
 * komponen di tes ini memakai layanan tiruan (`vi.fn().mockResolvedValue(...)`), yang tidak
 * terdaftar di sana. Dipanggil setelah `fixture.detectChanges()` pertama, sebelum membaca DOM.
 */
export async function flushAsync(putaran = 20): Promise<void> {
  for (let i = 0; i < putaran; i++) {
    await Promise.resolve();
  }
}
