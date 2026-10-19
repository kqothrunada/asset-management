import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, download, rupiah, type AssetRow, type Paged } from "../api";
import { useAuth } from "../auth";

export default function AssetList() {
    const nav = useNavigate();
    const { me, can } = useAuth();
    const qc = useQueryClient();
    const del = useMutation({
        mutationFn: (id: number) => api(`/api/assets/${id}`, { method: "DELETE" }),
        onSuccess: () => { setErr(""); qc.invalidateQueries({ queryKey: ["assets"] }); qc.invalidateQueries({ queryKey: ["dash"] }); },
        onError: (e: Error) => setErr(e.message),
    });
    const [q, setQ] = useState("");
    const [status, setStatus] = useState("");
    const [page, setPage] = useState(1);
    const [err, setErr] = useState("");

    const qs = new URLSearchParams({ page: String(page), pageSize: "10" });
    if (q.trim()) qs.set("q", q.trim());
    if (status) qs.set("status", status);

    const list = useQuery({ queryKey: ["assets", q, status, page], queryFn: () => api<Paged<AssetRow>>(`/api/assets?${qs}`) });
    const totalPages = Math.max(1, Math.ceil((list.data?.total ?? 0) / 10));
    const isHqManager = me?.isManager && me.division === "Headquarters";

    const exportXlsx = async () => {
        setErr("");
        try {
            const f = new URLSearchParams(qs); f.delete("page"); f.delete("pageSize");
            await download(`/api/assets/export?${f}`, "assets.xlsx");
        } catch (e) { setErr((e as Error).message); }
    };

    return (
        <>
            <div className="row between">
                <h1>Asset Management</h1>
                <div className="row">
                    {isHqManager && <button onClick={exportXlsx}>Export Excel</button>}
                    {can("assets", "add") && <button className="primary" onClick={() => nav("/assets/new")}>+ Tambah Asset</button>}
                </div>
            </div>
            {err && <div className="error">{err}</div>}

            <div className="row">
                <input placeholder="Cari ID atau nama…" value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} />
                <select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
                    <option value="">Semua status</option>
                    {["New", "UnderReview", "Assigned", "Returned", "Discarded"].map((s) => <option key={s}>{s}</option>)}
                </select>
            </div>

            <div className="card table-wrap">
                <table>
                    <thead><tr><th>ID</th><th>Name</th><th>Category</th><th>Subcategory</th><th>Status</th><th className="num">Total Value</th><th>Actions</th></tr></thead>
                    <tbody>
                        {list.isLoading && <tr><td colSpan={7}>Memuat…</td></tr>}
                        {list.error && <tr><td colSpan={7} className="error">{(list.error as Error).message}</td></tr>}
                        {list.data?.items.map((a) => (
                            <tr key={a.id}>
                                <td><Link to={`/assets/${a.id}?view=1`}>{a.assetCode}</Link></td>
                                <td>{a.name}</td><td>{a.category}</td><td>{a.subcategory}</td>
                                <td><span className={`badge ${a.status}`}>{a.status}</span></td>
                                <td className="num">{rupiah(a.assetValueTotal)}</td>
                                <td className="row">
                                    <button onClick={() => nav(`/assets/${a.id}?view=1`)}>View</button>
                                    {can("assets", "edit") && <button onClick={() => nav(`/assets/${a.id}`)}>Edit</button>}
                                    {can("assets", "delete") && (
                                        <button onClick={() => confirm(`Hapus ${a.assetCode}? Maintenance dan approval-nya ikut terhapus.`) && del.mutate(a.id)}>
                                            Delete
                                        </button>
                                    )}
                                </td>
                            </tr>
                        ))}
                        {list.data?.items.length === 0 && <tr><td colSpan={7}>Tidak ada data.</td></tr>}
                    </tbody>
                </table>
            </div>

            <div className="row">
                <button disabled={page <= 1} onClick={() => setPage(page - 1)}>‹</button>
                <span>Halaman {page} / {totalPages} · {list.data?.total ?? 0} data</span>
                <button disabled={page >= totalPages} onClick={() => setPage(page + 1)}>›</button>
            </div>
        </>
    );
}