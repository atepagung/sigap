import { isWithinBasePath } from './base-path';
import { defineRemoteElement } from './define-remote-element';
import { REMOTE_IDENTITY } from './generated/remote-identity';

/**
 * Mode mandiri (remote dibuka langsung di port-nya sendiri, tanpa shell). Sengaja memakai jalur
 * yang sama dengan saat dimuat shell — custom element — supaya perilaku routing identik.
 */
export async function runStandalone(): Promise<void> {
  const { routePath, elementName, displayName } = REMOTE_IDENTITY;
  // Tanpa shell, tidak ada yang mengatur judul tab — di dalam shell, judul milik shell.
  document.title = `${displayName} (mode mandiri)`;
  if (!isWithinBasePath(location.pathname, routePath)) {
    history.replaceState(null, '', routePath);
  }
  await defineRemoteElement();
  document.body.appendChild(document.createElement(elementName));
}
