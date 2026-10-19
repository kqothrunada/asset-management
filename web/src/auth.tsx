import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { api, getToken, setToken, type MenuItem } from "./api";

type Me = { id: string; name: string; division: string; isManager: boolean; roles: string[] };
type Action = "view" | "add" | "edit" | "delete";
type Ctx = {
    me: Me | null; menu: MenuItem[]; ready: boolean;
    login: (u: string, p: string) => Promise<void>; logout: () => void;
    can: (menuKey: string, action: Action) => boolean;
};
const AuthCtx = createContext<Ctx>(null!);
export const useAuth = () => useContext(AuthCtx);

export function AuthProvider({ children }: { children: ReactNode }) {
    const [me, setMe] = useState<Me | null>(() => JSON.parse(localStorage.getItem("me") || "null"));
    const [menu, setMenu] = useState<MenuItem[]>([]);
    const [ready, setReady] = useState(!getToken());

    useEffect(() => {
        if (getToken())
            api<MenuItem[]>("/api/me/menu").then(setMenu).catch(() => { }).finally(() => setReady(true));
    }, []);

    const login = async (userName: string, password: string) => {
        const r = await api("/api/auth/login", { method: "POST", body: { userName, password } });
        setToken(r.token);
        const m: Me = { id: r.id, name: r.name, division: r.division, isManager: r.isManager, roles: r.roles };
        localStorage.setItem("me", JSON.stringify(m));
        setMe(m);
        setMenu(await api<MenuItem[]>("/api/me/menu"));
        setReady(true);
    };
    const logout = () => { setToken(null); localStorage.removeItem("me"); setMe(null); setMenu([]); };
    const can = (key: string, action: Action) => menu.find((m) => m.key === key)?.[action] ?? false;

    return <AuthCtx.Provider value={{ me, menu, ready, login, logout, can }}>{children}</AuthCtx.Provider>;
}