// Menyusun baris skenario UAT untuk database development: gempa bumi M 6.4 Kab. Pasaman dengan
// KPP Madya Pekanbaru sebagai unit terdampak, seperti prototipe yang sudah disetujui.
//
// Murni: waktu dihitung dari `sekarang` yang diberikan pemanggil, tanpa I/O, supaya bisa dites.
// Semua baris `isDemo = true` dan berid `skenario-*` sehingga bisa dibuang dan dibuat ulang.
//
// Nilai kolom berskala memakai LABEL tersimpan (`Rusak Ringan`), bukan kode API: persis seperti
// yang ditulis AsesmenStore lewat ValidatorAsesmen.KeTersimpan.

export const ID = {
  broadcast: 'skenario-broadcast-gempa',
  layananLoket: 'skenario-layanan-loket',
  layananSistem: 'skenario-layanan-sistem',
  layananArsip: 'skenario-layanan-arsip',
  gangguan: 'skenario-gangguan-sistem',
};

export const KODE_UNIT_TERDAMPAK = 'DEMO-KPP-MADYA-PKU';
export const NIP_SATGAS = '900000000000000002';

const menit = (n) => n * 60_000;

/**
 * @param {object} p
 * @param {Date} p.sekarang
 * @param {string} p.unitId       KPP Madya Pekanbaru
 * @param {string} p.satgasId     pemicu broadcast, pengisi asesmen, pelapor gangguan
 * @param {string[]} p.pegawaiIds Pegawai Umum aktif di unit, urut NIP; tiga pertama sengaja belum menjawab
 */
export function bangunSkenario({ sekarang, unitId, satgasId, pegawaiIds }) {
  if (pegawaiIds.length < 6) throw new Error('Butuh minimal 6 Pegawai Umum di unit agar rekap bermakna.');
  const t = (menitLalu) => new Date(sekarang.getTime() - menit(menitLalu));

  const dipicu = t(180);
  const broadcast = {
    id: ID.broadcast,
    isDemo: true,
    jenisBencana: 'Gempa Bumi',
    kategoriBencana: 'ALAM',
    lokasi: 'Kab. Pasaman, Sumatera Barat',
    wilayah: 'Sumatera Barat',
    pesan: 'Gempa bumi M 6.4 dirasakan di Riau. Segera konfirmasi kondisi Anda: Saya Aman atau Butuh Bantuan.',
    dikirimOlehId: satgasId,
    isOverrideNasional: false,
    targetJenis: 'UNIT',
    targetUnitId: unitId,
    otomatis: false,
    createdAt: dipicu,
  };

  const sasaran = [{
    id: 'skenario-sasaran-madya', broadcastId: ID.broadcast, unitId, jenisBencana: broadcast.jenisBencana,
    status: 'DISASAR', aktif: true, isDemo: true, createdAt: dipicu,
  }];

  // Tiga pertama (termasuk akun uji Pegawai Umum) belum menjawab supaya "Safety Check Saya" bisa dicoba.
  const belum = 3;
  const jawaban = pegawaiIds.slice(belum).map((userId, i, sisa) => {
    const butuh = i >= sisa.length - 2;
    return {
      id: `skenario-jawab-${String(belum + i + 1).padStart(2, '0')}`,
      isDemo: true,
      userId,
      unitId,
      status: butuh ? 'BUTUH_BANTUAN' : 'AMAN',
      // Keterangan hanya ada pada jawaban yang dicatatkan Satgas (#6); jawaban sendiri (#2) mengosongkannya.
      keterangan: butuh ? 'Terjebak di lantai 3, tangga darurat tertutup puing.' : null,
      dicatatOlehId: butuh ? satgasId : null,
      broadcastId: ID.broadcast,
      createdAt: t(175 - i * 7),
    };
  });

  const layanan = [
    { id: ID.layananLoket, nama: 'Pelayanan Tatap Muka Wajib Pajak', rtoJam: 8 },
    { id: ID.layananSistem, nama: 'Sistem Informasi Perpajakan Kantor', rtoJam: 4 },
    { id: ID.layananArsip, nama: 'Penatausahaan Arsip Wajib Pajak', rtoJam: 24 },
  ].map((l) => ({ ...l, isDemo: true, unitId, kritis: true, createdAt: t(600), updatedAt: t(600) }));

  const gangguan = {
    id: ID.gangguan, isDemo: true, layananId: ID.layananSistem, status: 'TERGANGGU', mulai: t(90),
    keterangan: 'Jaringan kantor terputus setelah gempa, server lokal tidak terjangkau.',
    dilaporkanOlehId: satgasId, createdAt: t(90), updatedAt: t(90),
  };

  const layananTerdampak = (status) => JSON.stringify(
    layanan.map((l) => ({
      id: l.id, nama: l.nama, rtoJam: l.rtoJam,
      status: l.id === ID.layananSistem ? status : 'NORMAL',
    })));

  // Satu versi = satu baris di tiap tabel dengan createdAt yang SAMA PERSIS (aturan pasangan S5).
  const versi = (n, menitLalu, kondisiFisik, statusSistem, uraian, tik) => {
    const dibuat = t(menitLalu);
    return {
      damage: {
        id: `skenario-asesmen-v${n}`, isDemo: true, unitId, submittedById: satgasId,
        jenisBencana: broadcast.jenisBencana, kategoriBencana: 'ALAM', waktuKejadian: t(185),
        kondisiFisik, deskripsi: uraian,
        catatanPegawai: n === 1 ? 'Dua pegawai belum terhubung, sedang dihubungi lewat telepon.' : 'Semua pegawai terhubung; dua butuh bantuan evakuasi.',
        layananTerdampak: layananTerdampak(statusSistem), createdAt: dibuat,
      },
      checklist: {
        id: `skenario-checklist-v${n}`, isDemo: true, unitId, submittedById: satgasId, createdAt: dibuat,
        sdmJumlah: n === 1 ? '75%' : '100% Lengkap', sdmKorban: 'Tidak Ada', sdmFisik: 'Ada Luka Ringan', sdmPsikis: 'Trauma Ringan',
        sdmCatatan: null,
        asetGedungKonstruksi: 'Rusak Ringan', asetGedungAkses: 'Dapat Diakses',
        asetPeralatanKondisi: 'Rusak Ringan', asetPeralatanJumlah: 'Ada Sebagian',
        asetPerlengkapanKondisi: 'Normal', asetPerlengkapanJumlah: 'Lengkap',
        asetKendaraanLaik: 'Normal', asetKendaraanJumlah: 'Lengkap',
        asetCatatan: 'Retak rambut di dinding lantai 2.',
        arsipVital: 'Aman', arsipPenting: 'Rusak Ringan', arsipEvakuasi: 'Dapat Dilakukan', arsipCatatan: null,
        tikKomputerKondisi: tik.kondisi, tikKomputerJumlah: 'Ada Sebagian', tikJaringanAkses: tik.jaringan,
        tikJaringanPower: 'Tersedia via UPS/Genset', tikAplikasiUtama: tik.aplikasi,
        tikCatatan: n === 2 ? 'Ruang server dipindah ke lantai 1.' : null,
      },
    };
  };

  const v1 = versi(1, 160, 'Minor', 'NORMAL', 'Getaran kuat, retak rambut pada dinding, kegiatan dihentikan sementara.',
    { kondisi: 'Rusak Ringan', jaringan: 'Lambat', aplikasi: 'Berfungsi Sebagian' });
  const v2 = versi(2, 60, 'Berat', 'TERGANGGU', 'Jaringan terputus, layanan sistem informasi terganggu; evakuasi berjalan.',
    { kondisi: 'Rusak Sedang', jaringan: 'Terputus Total', aplikasi: 'Tidak Berfungsi' });

  // Peran, profil lingkup, dan unit pemicu bukan kolom "ActiveBroadcast": BroadcastStore menitipkannya di
  // "JejakPerubahan"."alasan" pada baris DIPICU, "{peran}|{profil}|{unitId}". Tanpa baris ini API
  // menampilkan peran pemicu sebagai "?".
  const jejakPemicu = {
    id: 'skenario-jejak-dipicu', isDemo: true, entitas: 'ActiveBroadcast', entitasId: ID.broadcast,
    aksi: 'DIPICU', alasan: `SATGAS|UNIT|${unitId}`, ringkasan: null, olehId: satgasId, createdAt: dipicu,
  };

  return {
    ActiveBroadcast: [broadcast],
    BroadcastSasaranUnit: sasaran,
    SafetyCheckResponse: jawaban,
    LayananKritis: layanan,
    GangguanLayanan: [gangguan],
    DamageAssessment: [v1.damage, v2.damage],
    ChecklistKondisiLapangan: [v1.checklist, v2.checklist],
    JejakPerubahan: [jejakPemicu],
  };
}

// Urutan penyisipan (induk dulu) dan urutan pembuangan (anak dulu).
export const URUTAN_TABEL = [
  'ActiveBroadcast', 'BroadcastSasaranUnit', 'SafetyCheckResponse',
  'LayananKritis', 'GangguanLayanan', 'DamageAssessment', 'ChecklistKondisiLapangan', 'JejakPerubahan',
];
