# Fikstur BMKG

Sumber data: **BMKG** (Badan Meteorologi, Klimatologi, dan Geofisika), Data Gempabumi Terbuka, `data.bmkg.go.id`.
Wajib menyebut BMKG sebagai sumber sesuai ketentuan data terbuka mereka.

| Berkas | Asal |
| --- | --- |
| `autogempa-asli-2026-09-24.json` | Respons **asli** `autogempa.json`, diambil 24 Sep 2026 |
| `gempadirasakan-asli-2026-09-24.json` | Respons **asli** `gempadirasakan.json` (15 kejadian), diambil 24 Sep 2026 |
| `kosong.json`, `gempa-bertipe-lain.json`, `campuran.json`, `rusak.json` | Buatan tangan: bentuk yang tidak lazim atau rusak |

Format aslinya tidak terdokumentasi rapi, jadi contoh nyata lebih berharga daripada asumsi (PLAYBOOK P5.1).
Pada hari pengambilan tidak ada kejadian yang mencapai MMI V; kasus MMI V dibuat di tes dari isian
"Dirasakan" yang diketik tangan.
