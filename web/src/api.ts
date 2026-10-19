const TOKEN_KEY = "token";
export const getToken = () => localStorage.getItem(TOKEN_KEY);
export const setToken = (t: string | null) =>
    t ? localStorage.setItem(TOKEN_KEY, t) : localStorage.removeItem(TOKEN_KEY);

export class ApiError extends Error {
    status: number;
    constructor(message: string, status: number) { super(message); this.status = status; }
}

async function toError(res: Response) {
    let body: any = null;
    try { body = await res.json(); } catch { /* bukan JSON */ }
    const fromErrors = body?.errors ? Object.values(body.errors).flat().join(" • ") : null;
    return new ApiError(
        body?.message ?? body?.detail ?? fromErrors ?? body?.title ?? `HTTP ${res.status} ${res.url}`, res.status);
}

export async function api<T = any>(path: string, opts: { method?: string; body?: unknown } = {}): Promise<T> {
    const token = getToken();
    const res = await fetch(path, {
        method: opts.method ?? "GET",
        headers: {
            ...(opts.body !== undefined ? { "Content-Type": "application/json" } : {}),
            ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
    });
    if (res.status === 401 && !path.includes("/auth/login")) {
        setToken(null);
        window.location.href = "/login";
        throw new ApiError("Sesi berakhir, silakan login ulang.", 401);
    }
    if (!res.ok) throw await toError(res);
    if (res.status === 204) return undefined as T;
    return res.json();
}

export async function download(path: string, filename: string) {
    const res = await fetch(path, { headers: { Authorization: `Bearer ${getToken()}` } });
    if (!res.ok) throw await toError(res);
    const url = URL.createObjectURL(await res.blob());
    const a = document.createElement("a");
    a.href = url; a.download = filename; a.click();
    URL.revokeObjectURL(url);
}

export const rupiah = (n: number) =>
    new Intl.NumberFormat("id-ID", { style: "currency", currency: "IDR", maximumFractionDigits: 2 }).format(n);

export type MenuItem = { key: string; title: string; route: string; view: boolean; add: boolean; edit: boolean; delete: boolean };
export type Paged<T> = { items: T[]; total: number; page: number; pageSize: number };
export type AssetRow = { id: number; assetCode: string; name: string; category: string; subcategory: string; status: string; assetValueTotal: number };
export type AssetDetail = {
    id: number; assetCode: string; name: string; description: string | null;
    requesterId: string; responsiblePersonId: string | null; category: string; subcategory: string; status: string;
    assetValueTotal: number; showMaintenance: boolean; requesterReadOnly: boolean; canEdit: boolean;
};
export type UserLookup = { id: string; fullName: string; division: string };
export type CategoryMap = { category: string; subcategories: { value: string; hasMaintenance: boolean }[] };