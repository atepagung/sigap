import { REMOTE_IDENTITY } from './generated/remote-identity';

// Golden Rule platform, contoh resmi dari slide: remoteUserManagement ->
// remote-user-management-element, defineRemoteUserManagementElement,
// app-remote-user-management-entry, /user-management, "User Management".
// Tes ini menjaga agar saat nilai resmi dari BaTII dimasukkan ke remote-identity.json,
// seluruh turunannya tetap konsisten satu sama lain.
describe('remote-identity.json mengikuti Golden Rule', () => {
  const { remoteName } = REMOTE_IDENTITY;
  const kebab = remoteName.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase();
  const tanpaAwalan = kebab.replace(/^remote-/, '');
  const pascal = remoteName.charAt(0).toUpperCase() + remoteName.slice(1);

  it('remoteName diawali "remote" dalam camelCase', () => {
    expect(remoteName).toMatch(/^remote[A-Z][A-Za-z0-9]*$/);
  });

  it('element name', () => {
    expect(REMOTE_IDENTITY.elementName).toBe(`${kebab}-element`);
  });

  it('function', () => {
    expect(REMOTE_IDENTITY.defineFunction).toBe(`define${pascal}Element`);
  });

  it('selector', () => {
    expect(REMOTE_IDENTITY.selector).toBe(`app-${kebab}-entry`);
  });

  it('route path', () => {
    expect(REMOTE_IDENTITY.routePath).toBe(`/${tanpaAwalan}`);
  });

  it('display name (huruf besar-kecil bebas, mis. akronim "SIGAP")', () => {
    expect(REMOTE_IDENTITY.displayName.toLowerCase()).toBe(tanpaAwalan.replace(/-/g, ' '));
  });
});
