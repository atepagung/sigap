import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { REMOTE_IDENTITY } from './generated/remote-identity';

/**
 * Wadah remote. Tiap kali shell membuat ulang elemen (mis. pengguna kembali ke modul ini dari
 * modul lain), router diselaraskan lagi dengan URL browser saat itu. `initialNavigation()`
 * aman dipanggil berulang: listener lokasi hanya dipasang sekali.
 */
@Component({
  selector: REMOTE_IDENTITY.selector,
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class RemoteEntryComponent {
  constructor() {
    inject(Router).initialNavigation();
  }
}
