import { useEffect, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { useAuth } from "../auth";
import Modal from "../components/Modal";

type U = { id: string; userName: string; fullName: string; email?: string; divisionId: number; divisionName: string; isManager: boolean; roles: string[] };
type R = { id: string; name: string; userCount: number };
type P = { menuKey: string; title: string; canView: boolean; canAdd: boolean; canEdit: boolean; canDelete: boolean };

export default function UserManagement() {
    const [tab, setTab] = useState<"users" | "roles" | "perms">("users");
    return (
        <>
            <h1>User Management</h1>
            <div className="tabs">
                <button className={tab === "users" ? "active" : ""} onClick={() => setTab("users")}>Users</button>
                <button className={tab === "roles" ? "active" : ""} onClick={() => setTab("roles")}>Roles</button>
                <button className={tab === "perms" ? "active" : ""} onClick={() => setTab("perms")}>Menu Permissions</button>
            </div>
            {tab === "users" && <UsersTab />}
            {tab === "roles" && <RolesTab />}
            {tab === "perms" && <PermsTab />}
        </>
    );
}

function UsersTab() {
    const qc = useQueryClient();
    const { can } = useAuth();
    const users = useQuery({ queryKey: ["users"], queryFn: () => api<U[]>("/api/users") });
    const roles = useQuery({ queryKey: ["roles"], queryFn: () => api<R[]>("/api/roles") });
    const divs = useQuery({ queryKey: ["divs"], queryFn: () => api<{ id: number; name: string }[]>("/api/lookups/divisions") });
    const [ed, setEd] = useState<any | null>(null);
    const [err, setErr] = useState("");

    const refresh = () => qc.invalidateQueries({ queryKey: ["users"] });
    const save = useMutation({
        mutationFn: () => ed.id
            ? api(`/api/users/${ed.id}`, { method: "PUT", body: { fullName: ed.fullName, email: ed.email || null, divisionId: Number(ed.divisionId), isManager: !!ed.isManager, roles: ed.roles, newPassword: ed.password || null } })
            : api("/api/users", { method: "POST", body: { userName: ed.userName, fullName: ed.fullName, email: ed.email || null, password: ed.password, divisionId: Number(ed.divisionId), isManager: !!ed.isManager, roles: ed.roles } }),
        onSuccess: () => { refresh(); setEd(null); setErr(""); },
        onError: (e: Error) => setErr(e.message),
    });
    const del = useMutation({
        mutationFn: (id: string) => api(`/api/users/${id}`, { method: "DELETE" }),
        onSuccess: () => { refresh(); setErr(""); }, onError: (e: Error) => setErr(e.message),
    });

    const toggleRole = (name: string) =>
        setEd((d: any) => ({ ...d, roles: d.roles.includes(name) ? d.roles.filter((r: string) => r !== name) : [...d.roles, name] }));
    const submit = (e: FormEvent) => { e.preventDefault(); save.mutate(); };

    return (
        <div className="card">
            <div className="row between">
                <h3>Users</h3>
                {can("users", "add") && <button className="primary" onClick={() => setEd({ userName: "", fullName: "", email: "", password: "", divisionId: divs.data?.[0]?.id ?? 1, isManager: false, roles: [] })}>+ Tambah User</button>}
            </div>
            {!ed && err && <div className="error">{err}</div>}
            <table>
                <thead><tr><th>Username</th><th>Nama</th><th>Divisi</th><th>Manager</th><th>Roles</th><th></th></tr></thead>
                <tbody>
                    {users.data?.map((u) => (
                        <tr key={u.id}>
                            <td>{u.userName}</td><td>{u.fullName}</td><td>{u.divisionName}</td><td>{u.isManager ? "Ya" : "-"}</td><td>{u.roles.join(", ")}</td>
                            <td className="row">
                                {can("users", "edit") && <button onClick={() => setEd({ ...u, password: "" })}>Edit</button>}
                                {can("users", "delete") && <button onClick={() => confirm(`Hapus ${u.userName}?`) && del.mutate(u.id)}>Delete</button>}
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>

            {ed && (
                <Modal title={ed.id ? "Edit User" : "Tambah User"} onClose={() => setEd(null)}>
                    <form className="form" onSubmit={submit}>
                        {err && <div className="error full">{err}</div>}
                        <label>Username<input value={ed.userName} disabled={!!ed.id} onChange={(e) => setEd({ ...ed, userName: e.target.value })} required /></label>
                        <label>Nama lengkap<input value={ed.fullName} onChange={(e) => setEd({ ...ed, fullName: e.target.value })} required /></label>
                        <label>Email<input type="email" value={ed.email ?? ""} onChange={(e) => setEd({ ...ed, email: e.target.value })} /></label>
                        <label>{ed.id ? "Password baru (opsional)" : "Password"}
                            <input type="password" value={ed.password} onChange={(e) => setEd({ ...ed, password: e.target.value })} required={!ed.id} />
                        </label>
                        <label>Divisi
                            <select value={ed.divisionId} onChange={(e) => setEd({ ...ed, divisionId: e.target.value })}>
                                {divs.data?.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
                            </select>
                        </label>
                        <label className="check"><input type="checkbox" checked={ed.isManager} onChange={(e) => setEd({ ...ed, isManager: e.target.checked })} /> Manager</label>
                        <div className="full">
                            <label>Roles</label>
                            <div className="row wrap">
                                {roles.data?.map((r) => (
                                    <label key={r.id} className="check"><input type="checkbox" checked={ed.roles.includes(r.name)} onChange={() => toggleRole(r.name)} /> {r.name}</label>
                                ))}
                            </div>
                        </div>
                        <div className="full row"><button className="primary" disabled={save.isPending}>Simpan</button><button type="button" onClick={() => setEd(null)}>Batal</button></div>
                    </form>
                </Modal>
            )}
        </div>
    );
}

function RolesTab() {
    const qc = useQueryClient();
    const { can } = useAuth();
    const roles = useQuery({ queryKey: ["roles"], queryFn: () => api<R[]>("/api/roles") });
    const [err, setErr] = useState("");
    const refresh = () => { qc.invalidateQueries({ queryKey: ["roles"] }); setErr(""); };
    const fail = (e: Error) => setErr(e.message);

    const create = useMutation({ mutationFn: (name: string) => api("/api/roles", { method: "POST", body: { name } }), onSuccess: refresh, onError: fail });
    const rename = useMutation({ mutationFn: (v: { id: string; name: string }) => api(`/api/roles/${v.id}`, { method: "PUT", body: { name: v.name } }), onSuccess: refresh, onError: fail });
    const del = useMutation({ mutationFn: (id: string) => api(`/api/roles/${id}`, { method: "DELETE" }), onSuccess: refresh, onError: fail });

    return (
        <div className="card">
            <div className="row between">
                <h3>Roles</h3>
                {can("users", "add") && <button className="primary" onClick={() => { const n = prompt("Nama role baru:"); if (n?.trim()) create.mutate(n.trim()); }}>+ Tambah Role</button>}
            </div>
            {err && <div className="error">{err}</div>}
            <table>
                <thead><tr><th>Role</th><th>Jumlah user</th><th></th></tr></thead>
                <tbody>
                    {roles.data?.map((r) => (
                        <tr key={r.id}>
                            <td>{r.name}</td><td>{r.userCount}</td>
                            <td className="row">
                                {can("users", "edit") && <button onClick={() => { const n = prompt("Nama baru:", r.name); if (n?.trim()) rename.mutate({ id: r.id, name: n.trim() }); }}>Rename</button>}
                                {can("users", "delete") && <button onClick={() => confirm(`Hapus role ${r.name}?`) && del.mutate(r.id)}>Delete</button>}
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

function PermsTab() {
    const { can } = useAuth();
    const roles = useQuery({ queryKey: ["roles"], queryFn: () => api<R[]>("/api/roles") });
    const [rid, setRid] = useState("");
    const [rows, setRows] = useState<P[]>([]);
    const [msg, setMsg] = useState("");

    const perms = useQuery({ queryKey: ["perms", rid], queryFn: () => api<P[]>(`/api/roles/${rid}/permissions`), enabled: !!rid });
    useEffect(() => { if (perms.data) { setRows(perms.data); setMsg(""); } }, [perms.data]);

    const toggle = (i: number, k: "canView" | "canAdd" | "canEdit" | "canDelete") =>
        setRows((r) => r.map((x, j) => (j === i ? { ...x, [k]: !x[k] } : x)));

    const save = useMutation({
        mutationFn: () => api(`/api/roles/${rid}/permissions`, {
            method: "PUT",
            body: rows.map(({ menuKey, canView, canAdd, canEdit, canDelete }) => ({ menuKey, canView, canAdd, canEdit, canDelete })),
        }),
        onSuccess: () => setMsg("Tersimpan. Berlaku langsung pada request berikutnya."),
        onError: (e: Error) => setMsg(e.message),
    });

    const editable = can("users", "edit");
    return (
        <div className="card">
            <h3>Hak akses menu per role</h3>
            <label>Role
                <select value={rid} onChange={(e) => setRid(e.target.value)}>
                    <option value="">— pilih role —</option>
                    {roles.data?.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
                </select>
            </label>
            {rid && (
                <>
                    <table>
                        <thead><tr><th>Menu</th><th>View</th><th>Add</th><th>Edit</th><th>Delete</th></tr></thead>
                        <tbody>
                            {rows.map((p, i) => (
                                <tr key={p.menuKey}>
                                    <td>{p.title}</td>
                                    {(["canView", "canAdd", "canEdit", "canDelete"] as const).map((k) => (
                                        <td key={k}><input type="checkbox" checked={p[k]} disabled={!editable} onChange={() => toggle(i, k)} /></td>
                                    ))}
                                </tr>
                            ))}
                        </tbody>
                    </table>
                    {msg && <div className={msg.startsWith("Tersimpan") ? "ok" : "error"}>{msg}</div>}
                    {editable && <button className="primary" onClick={() => save.mutate()} disabled={save.isPending}>Simpan</button>}
                </>
            )}
        </div>
    );
}