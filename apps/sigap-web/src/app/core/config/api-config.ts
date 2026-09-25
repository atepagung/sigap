import { InjectionToken } from '@angular/core';

/**
 * Alamat dasar sigap-api dan issuer Keycloak dev. [ASUMSI] nilai pengembangan lokal saja —
 * di platform sungguhan, gateway ICS dan penyerahan token mengikuti mekanisme shell
 * (DUMMY_REGISTRY bagian 3.3 butir 51, belum diketahui). Jangan dipakai sebagai tebakan URL
 * platform; ganti lewat provider ini saja saat mekanismenya diketahui.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => 'http://localhost:5299',
});

/** Issuer realm Keycloak dummy (infra/keycloak). Dipakai hanya oleh login dev — lihat core/auth. */
export const KEYCLOAK_ISSUER_URL = new InjectionToken<string>('KEYCLOAK_ISSUER_URL', {
  providedIn: 'root',
  factory: () => 'http://localhost:8081/realms/kemenkeu',
});

/**
 * Client `sigap-uji-lokal`: public, password grant, HANYA untuk pengembangan/pengujian lokal
 * (infra/keycloak/README.md — "BUKAN bagian permintaan ke BaTII").
 */
export const KEYCLOAK_DEV_CLIENT_ID = new InjectionToken<string>('KEYCLOAK_DEV_CLIENT_ID', {
  providedIn: 'root',
  factory: () => 'sigap-uji-lokal',
});
