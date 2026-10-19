import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, rupiah } from "../api";
import Modal from "./Modal";

type M = { id: number; assetId: number; cost: number; type: string; comments?: string; vendor?: string; date: string };
type Draft = { id?: number; cost: string; type: string; comments: string; vendor: string; date: string };
type Props = { assetId: number; assetCode: string; canEdit: boolean; onTotal: (n: number) => void };

const today = () => new Date().toISOString().slice(0, 10);

export default function MaintenanceTab({ assetId, assetCode, canEdit, onTotal }: Props) {
    const qc = useQueryClient();
    const key = ["maint", assetId];
    const list = useQuery({ queryKey: key, queryFn: () => api<M[]>(`/api/assets/${assetId}/maintenances`) });
    const [draft, setDraft] = useState<Draft | null>(null);
    const [err, setErr] = useState("");

    const done = (r: { assetValueTotal: number }) => {
        onTotal(r.assetValueTotal);
        qc.invalidateQueries({ queryKey: key });
        qc.invalidateQueries({ queryKey: ["assets"] });
        setDraft(null); setErr("");
    };

    const save = useMutation({
        mutationFn: (d: Draft) => {
            const body = { cost: Number(d.cost), type: d.type, comments: d.comments || null, vendor: d.vendor || null, date: d.date };
            return d.id
                ? api(`/api/assets/${assetId}/maintenances/${d.id}`, { method: "PUT", body })
                : api(`/api/assets/${assetId}/maintenances`, { method: "POST", body });
        },
        onSuccess: done, onError: (e: Error) => setErr(e.message),
    });
    const remove = useMutation({
        mutationFn: (id: number) => api(`/api/assets/${assetId}/maintenances/${id}`, { method: "DELETE" }),
        onSuccess: done, onError: (e: Error) => setErr(e.message),
    });

    const edit = (m: M) => setDraft({ id: m.id, cost: String(m.cost), type: m.type, comments: m.comments ?? "", vendor: m.vendor ?? "", date: m.date });
    const submit = (e: FormEvent) => { e.preventDefault(); if (draft) save.mutate(draft); };
    const up = (k: keyof Draft, v: string) => setDraft((d) => d && { ...d, [k]: v });

    return (
        <div className="card">
            <div className="row between">
                <h3>Maintenance Records</h3>
                {canEdit && <button className="primary" onClick={() => setDraft({ cost: "", type: "Preventive", comments: "", vendor: "", date: today() })}>+ Tambah Pemeliharaan</button>}
            </div>
            {!draft && err && <div className="error">{err}</div>}

            <table>
                <thead><tr><th>Tanggal</th><th>Jenis</th><th className="num">Biaya</th><th>Vendor</th><th>Komentar</th><th>Aksi</th></tr></thead>
                <tbody>
                    {list.data?.map((m) => (
                        <tr key={m.id}>
                            <td>{m.date}</td><td>{m.type}</td><td className="num">{rupiah(m.cost)}</td><td>{m.vendor}</td><td>{m.comments}</td>
                            <td className="row">
                                {canEdit && <>
                                    <button onClick={() => edit(m)}>Edit</button>
                                    <button onClick={() => confirm("Hapus record ini?") && remove.mutate(m.id)}>Delete</button>
                                </>}
                            </td>
                        </tr>
                    ))}
                    {list.data?.length === 0 && <tr><td colSpan={6}>Belum ada record maintenance.</td></tr>}
                </tbody>
            </table>

            {draft && (
                <Modal title={draft.id ? "Edit Pemeliharaan" : "Tambah Pemeliharaan"} onClose={() => setDraft(null)}>
                    <form className="form" onSubmit={submit}>
                        {err && <div className="error full">{err}</div>}
                        <label>Linked Asset<input value={assetCode} readOnly /></label>
                        <label>Date<input type="date" value={draft.date} onChange={(e) => up("date", e.target.value)} required /></label>
                        <label>Maintenance Type
                            <select value={draft.type} onChange={(e) => up("type", e.target.value)}>
                                {["Preventive", "Corrective", "Emergency"].map((t) => <option key={t}>{t}</option>)}
                            </select>
                        </label>
                        <label>Maintenance Cost<input type="number" min="0" step="0.01" value={draft.cost} onChange={(e) => up("cost", e.target.value)} required /></label>
                        <label className="full">Vendor<input value={draft.vendor} onChange={(e) => up("vendor", e.target.value)} maxLength={200} /></label>
                        <label className="full">Comments<textarea rows={3} value={draft.comments} onChange={(e) => up("comments", e.target.value)} /></label>
                        <div className="full row">
                            <button className="primary" disabled={save.isPending}>Simpan</button>
                            <button type="button" onClick={() => setDraft(null)}>Batal</button>
                        </div>
                    </form>
                </Modal>
            )}
        </div>
    );
}