import { withNativeFederation, shareAll } from '@angular-architects/native-federation/config';
import { readRemoteIdentity } from './scripts/remote-identity.mjs';

// Konfigurasi federation adalah tanggung jawab starter.mfe — ditinjau ulang saat P7.2.
// [ASUMSI] kunci exposes './web-components' dan kebijakan shared di bawah; lihat DUMMY_REGISTRY.md.
export default withNativeFederation({
  name: readRemoteIdentity().remoteName,

  exposes: {
    './web-components': './src/federation/generated/remote-entry.ts',
  },

  shared: {
    ...shareAll(
      { singleton: true, strictVersion: true, requiredVersion: 'auto', build: 'package' },
      {
        overrides: {
          '@angular/core': {
            singleton: true,
            strictVersion: true,
            requiredVersion: 'auto',
            build: 'package',
            includeSecondaries: { keepAll: true },
          },
        },
      },
    ),
  },

  skip: ['rxjs/ajax', 'rxjs/fetch', 'rxjs/testing', 'rxjs/webSocket'],

  features: {
    denseChunking: true,
  },
});
