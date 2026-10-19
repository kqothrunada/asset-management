import type { ReactNode } from "react";
import { Navigate, NavLink, Outlet, Route, Routes } from "react-router-dom";
import { getToken } from "./api";
import { useAuth } from "./auth";
import Login from "./pages/Login";
import Dashboard from "./pages/Dashboard";
import AssetList from "./pages/AssetList";
import AssetForm from "./pages/AssetForm";
import UserManagement from "./pages/UserManagement";

function Layout() {
    const { me, menu, logout, ready } = useAuth();
    if (!getToken()) return <Navigate to="/login" replace />;
    if (!ready) return <p style={{ padding: 24 }}>Memuat…</p>;
    return (
        <div className="shell">
            <aside className="sidebar">
                <h2>Asset Management</h2>
                <nav>
                    {menu.map((m) => (
                        <NavLink key={m.key} to={m.route} end={m.route === "/"}>{m.title}</NavLink>
                    ))}
                </nav>
                <div className="me">
                    <strong>{me?.name}</strong><br /><small>{me?.division}{me?.isManager ? " · Manager" : ""}</small>
                    <button onClick={logout}>Keluar</button>
                </div>
            </aside>
            <main><Outlet /></main>
        </div>
    );
}

function Guard({ menuKey, children }: { menuKey: string; children: ReactNode }) {
    const { can } = useAuth();
    return can(menuKey, "view") ? <>{children}</> : <div className="card">Anda tidak memiliki akses ke halaman ini.</div>;
}

export default function App() {
    return (
        <Routes>
            <Route path="/login" element={<Login />} />
            <Route element={<Layout />}>
                <Route index element={<Guard menuKey="dashboard"><Dashboard /></Guard>} />
                <Route path="assets" element={<Guard menuKey="assets"><AssetList /></Guard>} />
                <Route path="assets/new" element={<Guard menuKey="assets"><AssetForm /></Guard>} />
                <Route path="assets/:id" element={<Guard menuKey="assets"><AssetForm /></Guard>} />
                <Route path="users" element={<Guard menuKey="users"><UserManagement /></Guard>} />
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
    );
}