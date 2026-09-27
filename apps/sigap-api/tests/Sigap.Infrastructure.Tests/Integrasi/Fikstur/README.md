# Fikstur BMKG

Sumber data: **BMKG** (Badan Meteorologi, Klimatologi, dan Geofisika), Data Gempabumi Terbuka, `data.bmkg.go.id`.
Wajib menyebut BMKG sebagai sumber sesuai ketentuan data terbuka mereka. Berkas `bnpb-*`: **BNPB**, Satu Data Bencana
Indonesia (`data.bnpb.go.id`), lisensi Open Data Commons Attribution License: wajib menyebut BNPB sebagai sumber.

| Berkas | Asal |
| --- | --- |
| `autogempa-asli-2026-09-24.json` | Respons **asli** `autogempa.json`, diambil 24 Sep 2026 |
| `gempadirasakan-asli-2026-09-24.json` | Respons **asli** `gempadirasakan.json` (15 kejadian), diambil 24 Sep 2026 |
| `kosong.json`, `gempa-bertipe-lain.json`, `campuran.json`, `rusak.json` | Buatan tangan: bentuk yang tidak lazim atau rusak |
| `cap-rss-asli-2026-09-27.xml` | Respons **asli** RSS peringatan dini cuaca BMKG (`www.bmkg.go.id/alerts/nowcast/id/rss.xml`, 9 peringatan), 27 Sep 2026 |
| `cap-peringatan-asli-CKG-2026-09-27.xml`, `cap-peringatan-asli-CKU-2026-09-27.xml` | Berkas CAP 1.2 **asli** dua peringatan di RSS itu (Kalimantan Tengah satu poligon; Kalimantan Utara puluhan poligon, 20 KB) |
| `bnpb-resource-asli-2026-09-27.json`, `bnpb-datastore-asli-2026-09-27.json` | Respons **asli** CKAN BNPB `resource_show` dan `datastore_search` untuk "Rekapitulasi Jumlah Kejadian dan Dampak Bencana Menurut Provinsi 2025" (9 jenis + baris Total) |

Format aslinya tidak terdokumentasi rapi, jadi contoh nyata lebih berharga daripada asumsi (PLAYBOOK P5.1).
Pada hari pengambilan tidak ada kejadian yang mencapai MMI V; kasus MMI V dibuat di tes dari isian
"Dirasakan" yang diketik tangan.
