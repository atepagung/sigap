import { Routes, UrlMatcher } from '@angular/router';
import { REMOTE_REGISTRY } from '../registry/remote-registry';
import { Lobi } from './lobi';
import { RemoteHost } from './remote-host';

/** Mencocokkan route path remote beserta seluruh sub-path-nya; sisanya urusan router remote. */
function awalanRemote(routePath: string): UrlMatcher {
  const awalan = routePath.split('/').filter(Boolean);
  return (segments) =>
    awalan.every((bagian, i) => segments[i]?.path === bagian) ? { consumed: segments } : null;
}

export const routes: Routes = [
  { path: '', component: Lobi },
  ...REMOTE_REGISTRY.map((remote) => ({
    matcher: awalanRemote(remote.routePath),
    component: RemoteHost,
    data: { remote },
  })),
  { path: '**', redirectTo: '' },
];
