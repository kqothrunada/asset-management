import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";

type A = { id: number; role: string; approverName: string; status: string; notes?: string; decidedAt?: string; canDecide: boolean };

export default function ApprovalCard({ assetId }: { assetId: number }) {
    const qc = useQueryClient();
    const key = ["approvals", String(assetId)];
    const list = useQuery({ queryKey: key, queryFn: () => api<A[]>(`/api/assets/${assetId}/approvals`) });
    const [err, setErr] = useState("");

    const decide = useMutation({
        mutationFn: ({ id, approve, notes }: { id: number; approve: boolean; notes: string | null }) =>
            api(`/api/approvals/${id}/${approve ? "approve" : "reject"}`, { method: "POST", body: { notes } }),
        onSuccess: () => {
            setErr("");
            qc.invalidateQueries({ queryKey: key });
            qc.invalidateQueries({ queryKey: ["asset", String(assetId)] });     // status asset bisa berubah
            qc.invalidateQueries({ queryKey: ["assets"] });
        },
        onError: (e: Error) => setErr(e.message),
    });

    const approve = (a: A) => decide.mutate({ id: a.id, approve: true, notes: prompt("Catatan (opsional):") || null });
    const reject = (a: A) => {
        const notes = prompt("Alasan penolakan (wajib):");
        if (notes?.trim()) decide.mutate({ id: a.id, approve: false, notes });
    };

    return (
        <div className="card" style={{ marginTop: 16 }}>
            <h3>Approval List</h3>
            {err && <div className="error">{err}</div>}
            <table>
                <thead><tr><th>Role</th><th>User</th><th>Status</th><th>Notes</th><th></th></tr></thead>
                <tbody>
                    {list.data?.map((a) => (
                        <tr key={a.id}>
                            <td>{a.role}</td><td>{a.approverName}</td>
                            <td><span className={`badge ${a.status}`}>{a.status}</span></td>
                            <td>{a.notes}</td>
                            <td className="row">
                                {a.canDecide && <>
                                    <button className="primary" onClick={() => approve(a)}>Approve</button>
                                    <button onClick={() => reject(a)}>Reject</button>
                                </>}
                            </td>
                        </tr>
                    ))}
                    {list.data?.length === 0 && <tr><td colSpan={5}>Belum ada proses approval. Ubah status menjadi Under Review untuk memulai.</td></tr>}
                </tbody>
            </table>
        </div>
    );
}