export function isWithinBasePath(pathname: string, basePath: string): boolean {
  const base = basePath.replace(/\/+$/, '');
  return pathname === base || pathname.startsWith(`${base}/`);
}
