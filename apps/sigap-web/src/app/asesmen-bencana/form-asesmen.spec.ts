import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideIamPermissions } from '@danarakca/iam';
import { ReferensiService } from '../core/referensi/referensi.service';
import { flushAsync } from '../shared/testing/flush-async';
import { AsesmenService } from './asesmen.service';
import { FormAsesmen } from './form-asesmen';

@Component({ selector: 'app-halaman-kosong-uji', template: '' })
class HalamanKosong {}

/** Opsi minimal — satu field per aspek — cukup untuk menguji form yang data-driven. */
const OPSI = {
  'kondisiBencana.kondisiFisik': [{ kode: 'BERAT', label: 'Berat' }],
  'sdm.kelengkapanHadir': [{ kode: 'PENUH_100', label: '100% Lengkap' }],
  'aset.konstruksiBangunan': [{ kode: 'KOKOH', label: 'Kokoh' }],
  'tik.kondisiPerangkat': [{ kode: 'NORMAL', label: 'Normal' }],
  'arsip.arsipVital': [{ kode: 'AMAN', label: 'Aman' }],
  'layanan.status': [{ kode: 'NORMAL', label: 'Normal' }],
};

async function render(
  terkini: { asesmen: unknown; urutanBerikutnya: number } = { asesmen: null, urutanBerikutnya: 1 },
) {
  const referensi = {
    jenisBencanaAsync: vi.fn().mockResolvedValue({
      data: [{ kategori: 'ALAM', label: 'Bencana Alam', jenis: ['Gempa Bumi'] }],
    }),
    opsiAsesmenAsync: vi.fn().mockResolvedValue(OPSI),
  };
  const asesmen = {
    layananKritisAsync: vi.fn().mockResolvedValue({
      data: [
        { id: 'lk-1', nama: 'Layanan SP2D', rtoJam: 24, rtoLabel: '1 hari', sumber: 'MANUAL' },
      ],
    }),
    terkiniAsync: vi.fn().mockResolvedValue(terkini),
    kirimAsync: vi.fn().mockResolvedValue({ id: 'as-baru' }),
    revisiAsync: vi.fn().mockResolvedValue({ id: 'as-1' }),
  };

  TestBed.configureTestingModule({
    providers: [
      // Rute tujuan navigasi setelah kirim/revisi — tanpa ini `router.navigate` gagal (NG04002).
      provideRouter([{ path: 'detail-asesmen/:id', component: HalamanKosong }]),
      { provide: ReferensiService, useValue: referensi },
      { provide: AsesmenService, useValue: asesmen },
      provideIamPermissions(() =>
        Promise.resolve(['sigap:asesmen:create', 'sigap:asesmen:update']),
      ),
    ],
  });

  const fixture = TestBed.createComponent(FormAsesmen);
  fixture.detectChanges();
  await flushAsync();
  fixture.detectChanges();
  return { fixture, asesmen };
}

function pilih(
  fixture: { nativeElement: HTMLElement; detectChanges: () => void },
  selector: string,
  value: string,
): void {
  const el = fixture.nativeElement.querySelector(selector) as HTMLSelectElement;
  el.value = value;
  el.dispatchEvent(new Event('change'));
  fixture.detectChanges();
}

describe('FormAsesmen', () => {
  it('tombol berlabel "Kirim" saat belum ada asesmen berjalan', async () => {
    const { fixture } = await render();
    expect(fixture.nativeElement.textContent).toContain('Kirim asesmen baru');
    const tombol = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.trim().length,
    );
    expect(tombol?.textContent?.trim()).toBe('Kirim');
  });

  it('tombol nonaktif sampai seluruh field wajib terisi, lalu memanggil kirimAsync', async () => {
    const { fixture, asesmen } = await render();

    pilih(fixture, '#jenis', 'Gempa Bumi');
    pilih(fixture, '#kondisi-fisik', 'BERAT');
    let tombol = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.trim() === 'Kirim',
    ) as HTMLButtonElement;
    expect(tombol.disabled).toBe(true);

    pilih(fixture, '#sdm\\.kelengkapanHadir', 'PENUH_100');
    pilih(fixture, '#aset\\.konstruksiBangunan', 'KOKOH');
    pilih(fixture, '#tik\\.kondisiPerangkat', 'NORMAL');
    pilih(fixture, '#arsip\\.arsipVital', 'AMAN');

    tombol = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.trim() === 'Kirim',
    ) as HTMLButtonElement;
    expect(tombol.disabled).toBe(false);

    tombol.click();
    await flushAsync();

    expect(asesmen.kirimAsync).toHaveBeenCalledTimes(1);
    const isi = asesmen.kirimAsync.mock.calls[0][0];
    expect(isi.kondisiBencana.jenisBencana).toBe('Gempa Bumi');
    expect(isi.aspek.sdm.kelengkapanHadir).toBe('PENUH_100');
  });

  it('berpindah ke "Update Asesmen" bila seri sudah berjalan', async () => {
    const asesmenBerjalan = {
      id: 'as-1',
      unit: { id: 'u1', nama: 'Unit A', provinsi: null, kabupatenKota: null, eselonI: null },
      dikirimOleh: { id: 'p1', nama: 'Satgas A', nip: null, jabatan: null },
      dikirimPada: '2026-09-20T00:00:00Z',
      urutan: 1,
      kondisiBencana: {
        kategoriBencana: 'ALAM',
        jenisBencana: 'Gempa Bumi',
        waktuKejadian: null,
        kondisiFisik: 'BERAT',
        uraian: null,
      },
      aspek: {
        sdm: {
          kelengkapanHadir: 'PENUH_100',
          korbanJiwa: null,
          kondisiFisik: null,
          kondisiPsikis: null,
          catatanKondisiPegawai: null,
          catatanTambahan: null,
        },
        aset: {
          konstruksiBangunan: 'KOKOH',
          aksesLokasi: null,
          kondisiPeralatan: null,
          jumlahPeralatan: null,
          kondisiPerlengkapan: null,
          jumlahPerlengkapan: null,
          kendaraanLaikOperasi: null,
          jumlahKendaraan: null,
          catatan: null,
        },
        tik: {
          kondisiPerangkat: 'NORMAL',
          jumlahPerangkat: null,
          aksesJaringan: null,
          kelistrikan: null,
          aplikasiUtama: null,
          catatan: null,
        },
        arsip: { arsipVital: 'AMAN', arsipPenting: null, evakuasiFisik: null, catatan: null },
        layanan: [],
      },
      persetujuan: {
        status: 'MENUNGGU_PIMPINAN',
        disetujuiOleh: null,
        disetujuiPada: null,
        tanggapDarurat: null,
      },
      lampiran: [],
    };

    const { fixture, asesmen } = await render({ asesmen: asesmenBerjalan, urutanBerikutnya: 2 });

    expect(fixture.nativeElement.textContent).toContain('Memperbarui asesmen versi #2');
    const tombol = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.trim() === 'Update Asesmen',
    ) as HTMLButtonElement;
    expect(tombol.disabled).toBe(false);

    tombol.click();
    await flushAsync();

    expect(asesmen.revisiAsync).toHaveBeenCalledWith('as-1', expect.anything());
  });
});
