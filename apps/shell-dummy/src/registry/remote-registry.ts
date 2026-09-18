import sigapBencana from '../../../sigap-web/remote-identity.json';

/**
 * [DUMMY] Registry modul. Di platform ICS, daftar remote diatur lewat konfigurasi server
 * (termasuk kill-switch). Di sini daftarnya disusun saat build dari berkas identitas milik
 * masing-masing remote, supaya nilai identitas tetap tinggal di SATU berkas.
 */
export interface RemoteRegistration {
  readonly remoteName: string;
  readonly remoteEntry: string;
  readonly exposedModule: string;
  readonly elementName: string;
  readonly defineFunction: string;
  readonly routePath: string;
  readonly displayName: string;
}

interface RemoteIdentity {
  readonly remoteName: string;
  readonly elementName: string;
  readonly defineFunction: string;
  readonly routePath: string;
  readonly displayName: string;
  readonly port: number;
}

// [ASUMSI] Kunci modul yang diekspos setiap remote; lihat DUMMY_REGISTRY.md.
const EXPOSED_MODULE = './web-components';

function register(identity: RemoteIdentity): RemoteRegistration {
  return {
    remoteName: identity.remoteName,
    remoteEntry: `http://localhost:${identity.port}/remoteEntry.json`,
    exposedModule: EXPOSED_MODULE,
    elementName: identity.elementName,
    defineFunction: identity.defineFunction,
    routePath: identity.routePath,
    displayName: identity.displayName,
  };
}

export const REMOTE_REGISTRY: readonly RemoteRegistration[] = [register(sigapBencana)];

export function federationManifest(): Record<string, string> {
  return Object.fromEntries(REMOTE_REGISTRY.map((r) => [r.remoteName, r.remoteEntry]));
}
