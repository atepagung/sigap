import { mergeApplicationConfig } from '@angular/core';
import { createCustomElement } from '@angular/elements';
import { createApplication } from '@angular/platform-browser';
import { appConfig } from '../app/app.config';
import { remoteFederationConfig } from './federation-providers';
import { REMOTE_IDENTITY } from './generated/remote-identity';
import { RemoteEntryComponent } from './remote-entry.component';

let definition: Promise<void> | undefined;

/**
 * Mendaftarkan custom element remote. Diekspos ke shell dengan nama fungsi dari
 * remote-identity.json (lihat generated/remote-entry.ts).
 *
 * [ASUMSI] Fungsi ini async dan idempoten; shell diasumsikan meng-await-nya sebelum membuat
 * elemen. Lihat DUMMY_REGISTRY.md.
 */
export function defineRemoteElement(): Promise<void> {
  definition ??= register();
  return definition;
}

async function register(): Promise<void> {
  const { elementName } = REMOTE_IDENTITY;
  if (customElements.get(elementName)) {
    return;
  }
  const app = await createApplication(mergeApplicationConfig(appConfig, remoteFederationConfig));
  customElements.define(
    elementName,
    createCustomElement(RemoteEntryComponent, { injector: app.injector }),
  );
}
