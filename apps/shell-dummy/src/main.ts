import { initFederation } from '@angular-architects/native-federation';
import { federationManifest } from './registry/remote-registry';

initFederation(federationManifest(), {
  hostRemoteEntry: { url: './remoteEntry.json' },
})
  .catch((err) => console.error(err))
  .then((_) => import('./bootstrap'))
  .catch((err) => console.error(err));
