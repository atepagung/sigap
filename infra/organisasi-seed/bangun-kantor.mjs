// Menyusun unit vertikal DUMMY (KPP/KPPN/KPPBC/...) dari data master gedung "KantorBmn", dan tautan
// "KantorBmn"."unitId"-nya. Murni: tanpa I/O database, supaya bisa dites tanpa PostgreSQL.
//
// [ASUMSI] Di platform asli data organisasi sudah memuat unit sampai lokasi kantor dan ditarik lewat API
// (keputusan pemilik, 25 Sep 2026). Berkas ini hanya pengganti sementara supaya pemicu otomatis BMKG punya
// unit berwilayah untuk dicocokkan; kode aplikasi tidak boleh bergantung pada cara unit ini dibuat.

// Nama Eselon I di data BMN -> kode unit OTK (induk unit vertikal). Nama tidak dikenal = dilaporkan, bukan ditebak.
export const PETA_ESELON1 = {
  'Direktorat Jenderal Pajak': 'djp',
  'Direktorat Jenderal Perbendaharaan': 'djpb',
  'Direktorat Jenderal Bea dan Cukai': 'djbc',
  'Direktorat Jenderal Kekayaan Negara': 'djkn',
  'Sekretariat Jenderal': 'setjen',
  'Badan Pendidikan dan Pelatihan Keuangan': 'bppk',
  'Badan Teknologi Informasi dan Intelijen Keuangan': 'batii',
  'Ditjen Pengelolaan Pembiayaan dan Risiko': 'djppr',
};

const kodeUnit = (kodeSatker) => `DEMO-SATKER-${kodeSatker}`;

/** Nilai terbanyak; seri dipecah menurut urutan abjad supaya hasil tidak bergantung urutan baris. */
function modus(nilai) {
  const hitung = new Map();
  for (const n of nilai) hitung.set(n, (hitung.get(n) ?? 0) + 1);
  return [...hitung.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))[0][0];
}

/**
 * @param {Array<{id:string,kodeSatker:string|null,namaSatker:string|null,eselon1:string|null,provinsi:string|null,kabkota:string|null}>} gedung
 * @returns {{unit: object[], tautan: Array<{kantorId:string,unitKode:string}>, laporan: object}}
 */
export function bangunUnitKantor(gedung) {
  const perSatker = new Map();
  const tanpaData = [];
  const eselonTakDikenal = new Map();

  for (const g of gedung) {
    if (!g.kodeSatker || !g.namaSatker || !g.kabkota) {
      tanpaData.push(g.id);
      continue;
    }
    if (!PETA_ESELON1[g.eselon1 ?? '']) {
      eselonTakDikenal.set(g.eselon1 ?? '(kosong)', (eselonTakDikenal.get(g.eselon1 ?? '(kosong)') ?? 0) + 1);
      tanpaData.push(g.id);
      continue;
    }
    (perSatker.get(g.kodeSatker) ?? perSatker.set(g.kodeSatker, []).get(g.kodeSatker)).push(g);
  }

  const unit = [];
  const tautan = [];
  const lebihDariSatuKabkota = [];
  for (const [kodeSatker, daftar] of [...perSatker.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
    const kabkota = new Set(daftar.map((g) => g.kabkota));
    // Satu unit hanya punya satu kabkota: yang terbanyak dipakai, sisanya dilaporkan untuk ditinjau pemilik.
    if (kabkota.size > 1) lebihDariSatuKabkota.push({ kodeSatker, kabkota: [...kabkota].sort() });
    const nama = modus(daftar.map((g) => g.namaSatker));
    const eselonIKey = PETA_ESELON1[daftar[0].eselon1];
    unit.push({
      id: `unit-demo-satker-${kodeSatker}`,
      kode: kodeUnit(kodeSatker),
      nama,
      tipe: nama.split(' ')[0],
      tingkat: 'INSTANSI_VERTIKAL',
      eselonIKey,
      provinsi: daftar.some((g) => g.provinsi) ? modus(daftar.map((g) => g.provinsi).filter(Boolean)) : null,
      kabkota: modus(daftar.map((g) => g.kabkota)),
      parentUnitId: `otk-${eselonIKey}`,
      isDemo: true,
    });
    for (const g of daftar) tautan.push({ kantorId: g.id, unitKode: kodeUnit(kodeSatker) });
  }

  return { unit, tautan, laporan: { jumlahGedung: gedung.length, tanpaData, eselonTakDikenal: [...eselonTakDikenal], lebihDariSatuKabkota } };
}
