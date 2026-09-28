/**
 * Public API dummy pengganti directive `*hasPermission` platform ICS.
 *
 * Kode aplikasi mengimpor lewat alias `@danarakca/iam` (tsconfig.base.json). [ASUMSI] nama
 * paket asli belum diketahui — lihat DUMMY_REGISTRY.md.
 */
export { HasPermissionDirective } from './src/has-permission.directive';
export { IamPermissions, provideIamPermissions } from './src/iam-permissions';
