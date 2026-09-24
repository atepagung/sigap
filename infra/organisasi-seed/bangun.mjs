// Menyusun baris "Unit", "User", dan "UserRole" untuk database development. Murni: tanpa I/O
// database, supaya bisa dites tanpa PostgreSQL.
//
// Tiga sumber:
//   1. OTK asli Kemenkeu (otk_bundle.json, bagian otk.unit) sampai Eselon III -> isDemo = false.
//   2. Lima unit demo yang dirujuk klaim `kode_satker` akun uji Keycloak -> isDemo = true.
//   3. Sepuluh akun uji dari realm Keycloak (NIP 9000...) + pegawai pelengkap -> isDemo = true.

export const PETA_TINGKAT = {
  Kementerian: 'KEMENTERIAN',
  'Eselon I': 'ESELON_I',
  'Staf Ahli': 'STAF_AHLI',
  'Eselon II': 'ESELON_II',
  'Eselon III': 'ESELON_III',
  'Eselon IV': 'ESELON_IV',
};

export const PETA_GRUP_KE_PERAN = {
  '/sigap-pegawai': 'PEGAWAI',
  '/sigap-satgas': 'SATGAS',
  '/sigap-pimpinan': 'PIMPINAN',
  '/sigap-perwakilan': 'PERWAKILAN',
  '/sigap-subkoordinator': 'SUBKOORDINATOR',
  '/sigap-koordinator': 'KOORDINATOR',
  '/sigap-sekjen': 'SEKJEN',
  '/sigap-admin': 'ADMIN',
  '/sigap-pengembang': 'PENGEMBANG',
  '/sigap-impl-rkb': 'IMPL_RKB',
};

// Sama seperti prototipe: yang dimuat sampai Eselon III, Eselon IV sengaja tidak.
const LEVEL_TERDALAM = 3;

const NAMA_PEGAWAI_PELENGKAP = [
  'Budi Santoso', 'Dewi Kusuma', 'Hendra Wijaya', 'Nur Fadilah', 'Siti Rahayu', 'Ahmad Fauzi',
  'Retno Wulandari', 'Dimas Pratama', 'Yusuf Hakim', 'Maya Anggraini', 'Rudi Hartono', 'Fitri Handayani',
];

export const UNIT_DEMO = [
  { id: 'unit-demo-pekanbaru', kode: 'DEMO-KPP-MADYA-PKU', nama: 'KPP Madya Pekanbaru', tipe: 'KPP Madya', tingkat: 'INSTANSI_VERTIKAL', eselonIKey: 'djp', provinsi: 'Riau', kabkota: 'Kota Pekanbaru' },
  { id: 'unit-demo-kanwil-riau', kode: 'DEMO-KANWIL-RIAU', nama: 'Kanwil Kemenkeu Provinsi Riau', tipe: 'Kanwil Terpadu', tingkat: 'ESELON_II', eselonIKey: 'setjen', provinsi: 'Riau', kabkota: 'Kota Pekanbaru' },
  { id: 'unit-demo-kppn-pekanbaru', kode: 'DEMO-KPPN-PKU', nama: 'KPPN Pekanbaru', tipe: 'KPPN', tingkat: 'INSTANSI_VERTIKAL', eselonIKey: 'djpb', provinsi: 'Riau', kabkota: 'Kota Pekanbaru' },
  { id: 'unit-demo-tampan', kode: 'DEMO-KPP-PRATAMA-TAMPAN', nama: 'KPP Pratama Pekanbaru Tampan', tipe: 'KPP Pratama', tingkat: 'INSTANSI_VERTIKAL', eselonIKey: 'djp', provinsi: 'Riau', kabkota: 'Kota Pekanbaru' },
  { id: 'unit-demo-senapelan', kode: 'DEMO-KPP-PRATAMA-SENAPELAN', nama: 'KPP Pratama Pekanbaru Senapelan', tipe: 'KPP Pratama', tingkat: 'INSTANSI_VERTIKAL', eselonIKey: 'djp', provinsi: 'Riau', kabkota: 'Kota Pekanbaru' },
];

/**
 * Akun layanan pengirim broadcast otomatis BMKG (ACCESS_RULES A11): baris "User" TANPA "UserRole", aktif
 * supaya resolver identitas menemukannya. Bukan pengguna demo (isDemo = false): di production baris yang
 * sama dibuat pemilik lewat SQL di README, dan NIP-nya harus sama dengan `Bmkg:NipLayanan`. Hanya untuk unit
 * akar Kemenkeu; tidak pernah dihitung sebagai pegawai (penyebut rekap membaca "UserRole" PEGAWAI).
 */
export const AKUN_LAYANAN_BMKG = { id: 'user-layanan-bmkg', nip: 'SISTEM-BMKG', nama: 'Sistem BMKG (akun layanan)', unitKode: 'kemenkeu' };

const idUnitOtk = (unitKey) => `otk-${unitKey}`;

function eselonIDari(unit, indeks) {
  let kini = unit;
  while (kini) {
    if (kini.level === 1) return kini.unit_key;
    if (kini.parent === null) return null;
    kini = indeks.get(kini.parent);
  }
  return null;
}

export function bangunUnitOtk(bundle) {
  const semua = bundle?.otk?.unit ?? [];
  if (semua.length === 0) throw new Error('Berkas OTK tidak memuat otk.unit.');
  const indeks = new Map(semua.map((u) => [u.id, u]));
  const dipakai = semua.filter((u) => u.level <= LEVEL_TERDALAM);
  const kunci = new Set(dipakai.map((u) => u.unit_key));
  if (kunci.size !== dipakai.length) throw new Error('unit_key OTK tidak unik; kolom "kode" harus unik.');

  return dipakai.map((u) => {
    const induk = u.parent === null ? undefined : indeks.get(u.parent);
    return {
      id: idUnitOtk(u.unit_key),
      kode: u.unit_key,
      nama: u.nama,
      tipe: u.jenis,
      tingkat: PETA_TINGKAT[u.jenis] ?? 'NON_ESELON',
      eselonIKey: eselonIDari(u, indeks),
      provinsi: null,
      kabkota: null,
      parentUnitId: induk && induk.level <= LEVEL_TERDALAM ? idUnitOtk(induk.unit_key) : null,
      isDemo: false,
    };
  });
}

export function bangunAkunUji(realm, unitPerKode) {
  const baris = [];
  for (const u of realm.users ?? []) {
    const nip = u.attributes?.nip?.[0];
    const kodeSatker = u.attributes?.kode_satker?.[0];
    if (!nip || !kodeSatker) throw new Error(`Akun ${u.username} di realm tidak punya nip/kode_satker.`);
    const unit = unitPerKode.get(kodeSatker);
    if (!unit) throw new Error(`Akun ${nip}: unit "${kodeSatker}" tidak ada di OTK maupun unit demo.`);
    const peran = (u.groups ?? []).map((g) => {
      const p = PETA_GRUP_KE_PERAN[g];
      if (!p) throw new Error(`Akun ${nip}: grup "${g}" tidak dikenal.`);
      return p;
    });
    baris.push({ id: `user-demo-${nip}`, nip, nama: `${u.firstName} ${u.lastName}`.trim(), unitId: unit.id, peran });
  }
  return baris;
}

export function bangunPegawaiPelengkap(unitId, jumlah = NAMA_PEGAWAI_PELENGKAP.length) {
  return Array.from({ length: jumlah }, (_, i) => {
    const nip = `9000000000000001${String(i + 1).padStart(2, '0')}`;
    return { id: `user-demo-${nip}`, nip, nama: NAMA_PEGAWAI_PELENGKAP[i % NAMA_PEGAWAI_PELENGKAP.length], unitId, peran: ['PEGAWAI'] };
  });
}

export function bangunSemua(bundle, realm) {
  const unitOtk = bangunUnitOtk(bundle);
  const unitDemo = UNIT_DEMO.map((u) => ({ ...u, parentUnitId: null, isDemo: true }));
  const unit = [...unitOtk, ...unitDemo];
  const perKode = new Map(unit.map((u) => [u.kode, u]));
  const akun = bangunAkunUji(realm, perKode);
  const pelengkap = bangunPegawaiPelengkap(perKode.get('DEMO-KPP-MADYA-PKU').id);
  const unitLayanan = perKode.get(AKUN_LAYANAN_BMKG.unitKode);
  if (!unitLayanan) throw new Error(`Unit akar "${AKUN_LAYANAN_BMKG.unitKode}" untuk akun layanan tidak ada di OTK.`);
  const layanan = { id: AKUN_LAYANAN_BMKG.id, nip: AKUN_LAYANAN_BMKG.nip, nama: AKUN_LAYANAN_BMKG.nama, unitId: unitLayanan.id };
  return { unit, pengguna: [...akun, ...pelengkap], layanan };
}
