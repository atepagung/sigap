import {
  APP_BASE_HREF,
  Location,
  LocationChangeListener,
  PathLocationStrategy,
  PlatformLocation,
} from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { isWithinBasePath } from './base-path';

/**
 * [ASUMSI] Tafsiran kita atas "Silent Location Strategy" di slide arsitektur ICS; implementasi
 * resminya ikut starter.mfe. Lihat DUMMY_REGISTRY.md.
 *
 * Router remote hidup terus selama tab terbuka, termasuk saat shell sedang menampilkan modul
 * lain. Strategi ini membuat router remote "diam" terhadap perubahan URL di luar wilayahnya
 * (route path remote), supaya tidak mencoba mencocokkan URL milik modul lain.
 */
@Injectable()
export class SilentLocationStrategy extends PathLocationStrategy {
  private readonly browserLocation: PlatformLocation;

  constructor() {
    const platformLocation = inject(PlatformLocation);
    super(platformLocation, inject(APP_BASE_HREF));
    this.browserLocation = platformLocation;
  }

  override onPopState(fn: LocationChangeListener): void {
    super.onPopState((event) => {
      if (isWithinBasePath(this.browserLocation.pathname, this.getBaseHref())) {
        fn(event);
      }
    });
  }

  /**
   * Akar remote ditulis `/<route-path>`, bukan `/<route-path>/`, supaya sama dengan URL yang
   * ditulis router shell. Kalau berbeda, kedua router saling menulis ulang URL.
   */
  override prepareExternalUrl(internal: string): string {
    return Location.stripTrailingSlash(super.prepareExternalUrl(internal)) || '/';
  }
}
