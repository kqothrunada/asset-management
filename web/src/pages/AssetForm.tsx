import { useEffect, useState, type FormEvent } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, rupiah, type AssetDetail, type CategoryMap, type UserLookup } from "../api";
import { useAuth } from "../auth";
import RichText from "../components/RichText";
import MaintenanceTab from "../components/MaintenanceTab";
import ApprovalCard from "../components/ApprovalCard";

const STATUSES = ["New", "UnderReview", "Assigned", "Returned", "Discarded"];
const ALLOWED: Record<string, string[]> = {      // cermin aturan server (AssetRules.IsManualTransitionAllowed)
    New: ["New", "UnderReview"], UnderReview: ["UnderReview"],
    Assigned: ["Assigned", "Returned"], Returned: ["Returned", "UnderReview"], Discarded: ["Discarded"],
};
const FURNITURE = ["Chair", "Table", "Cabinet"];
const stripHtml = (h: string) => h.replace(/<[^>]*>/g, "").replace(/&nbsp;/g, " ").trim();

type Form = { name: string; description: string; requesterId: string; responsiblePersonId: string; category: string; subcategory: string; status: string };

export default function AssetForm() {
    const { id } = useParams();
    const isNew = !id;
    const [sp] = useSearchParams();
    const nav = useNavigate();
    const qc = useQueryClient();
    const { me } = useAuth();

    const [form, setForm] = useState<Form>({ name: "", description: "", requesterId: me?.id ?? "", responsiblePersonId: "", category: "ITEquipment", subcategory: "Laptop", status: "New" });
    const [tab, setTab] = useState<"detail" | "maint">("detail");
    const [total, setTotal] = useState(0);
    const [err, setErr] = useState("");
    const [saved, setSaved] = useState(false);

    const users = useQuery({ queryKey: ["lk-users"], queryFn: () => api<UserLookup[]>("/api/lookups/users") });
    const cats = useQuery({ queryKey: ["lk-cats"], queryFn: () => api<CategoryMap[]>("/api/lookups/categories") });
    const detail = useQuery({ queryKey: ["asset", id], queryFn: () => api<AssetDetail>(`/api/assets/${id}`), enabled: !!id });

    useEffect(() => {
        const d = detail.data;
        if (!d) return;
        setForm({
            name: d.name, description: d.description ?? "", requesterId: d.requesterId, responsiblePersonId: d.responsiblePersonId ?? "",
            category: d.category, subcategory: d.subcategory, status: d.status
        });
        setTotal(d.assetValueTotal);
    }, [detail.data]);

    const set = <K extends keyof Form>(k: K, v: Form[K]) => { setForm((f) => ({ ...f, [k]: v })); setSaved(false); };
    const subs = cats.data?.find((c) => c.category === form.category)?.subcategories ?? [];

    // ---- aturan UI ----
    const readOnly = !isNew && (sp.get("view") === "1" || detail.data?.canEdit === false);
    const showMaintenance = !isNew && !FURNITURE.includes(form.subcategory);        // Chair/Table/Cabinet → tab disembunyikan
    const requesterLocked = readOnly || form.status === "Assigned" || !!detail.data?.requesterReadOnly;
    const descriptionRequired = form.status === "New";
    const currentStatus = detail.data?.status ?? "New";

    const save = useMutation({
        mutationFn: () => {
            const body = { ...form, requesterId: form.requesterId || null, responsiblePersonId: form.responsiblePersonId || null };
            return isNew
                ? api<AssetDetail>("/api/assets", { method: "POST", body })
                : api<AssetDetail>(`/api/assets/${id}`, { method: "PUT", body });
        },
        onSuccess: (d) => {
            setErr(""); setSaved(true);
            qc.invalidateQueries({ queryKey: ["assets"] });
            qc.invalidateQueries({ queryKey: ["approvals", String(d.id)] });
            qc.setQueryData(["asset", String(d.id)], d);
            if (isNew) nav(`/assets/${d.id}`, { replace: true });
        },
        onError: (e: Error) => { setErr(e.message); setSaved(false); },
    });

    const submit = (e: FormEvent) => {
        e.preventDefault();
        if (descriptionRequired && !stripHtml(form.description)) return setErr("Description wajib diisi saat status New.");
        if (!form.name.trim()) return setErr("Name wajib diisi.");
        save.mutate();
    };

    if (!isNew && detail.isLoading) return <p>Memuat…</p>;
    if (detail.error) return <div className="error">{(detail.error as Error).message}</div>;

    return (
        <>
            <div className="row between">
                <h1>{isNew ? "Tambah Asset" : `${detail.data?.assetCode} — ${readOnly ? "Detail" : "Edit"}`}</h1>
                <button onClick={() => nav("/assets")}>← Kembali</button>
            </div>

            <div className="tabs">
                <button className={tab === "detail" ? "active" : ""} onClick={() => setTab("detail")}>Detail</button>
                {showMaintenance && <button className={tab === "maint" ? "active" : ""} onClick={() => setTab("maint")}>Maintenance</button>}
            </div>

            {tab === "detail" && (
                <form className="card form" onSubmit={submit}>
                    {err && <div className="error">{err}</div>}
                    {saved && <div className="ok">Tersimpan.</div>}

                    <label>Asset ID<input value={detail.data?.assetCode ?? "(otomatis: AST-xxx)"} readOnly /></label>
                    <label>Asset Name *<input value={form.name} onChange={(e) => set("name", e.target.value)} disabled={readOnly} maxLength={200} /></label>

                    <div className="full">
                        <label>Description {descriptionRequired && <span className="req">*</span>}</label>
                        <RichText value={form.description} onChange={(v) => set("description", v)} disabled={readOnly} />
                    </div>

                    <label>Requester
                        <select value={form.requesterId} onChange={(e) => set("requesterId", e.target.value)} disabled={requesterLocked}>
                            {users.data?.map((u) => <option key={u.id} value={u.id}>{u.fullName} ({u.division})</option>)}
                        </select>
                        {form.status === "Assigned" && <small>Read-only karena status Assigned.</small>}
                    </label>

                    <label>Responsible Person
                        <select value={form.responsiblePersonId} onChange={(e) => set("responsiblePersonId", e.target.value)} disabled={readOnly}>
                            <option value="">— pilih —</option>
                            {users.data?.map((u) => <option key={u.id} value={u.id}>{u.fullName} ({u.division})</option>)}
                        </select>
                    </label>

                    <label>Category
                        <select value={form.category} disabled={readOnly}
                            onChange={(e) => {
                                const first = cats.data?.find((c) => c.category === e.target.value)?.subcategories[0]?.value ?? "";
                                setForm((f) => ({ ...f, category: e.target.value, subcategory: first })); setSaved(false);
                            }}>
                            {cats.data?.map((c) => <option key={c.category} value={c.category}>{c.category}</option>)}
                        </select>
                    </label>

                    <label>Subcategory
                        <select value={form.subcategory} onChange={(e) => set("subcategory", e.target.value)} disabled={readOnly}>
                            {subs.map((s) => <option key={s.value}>{s.value}</option>)}
                        </select>
                    </label>

                    <label>Status
                        <select value={form.status} onChange={(e) => set("status", e.target.value)} disabled={readOnly || isNew}>
                            {STATUSES.map((s) => <option key={s} disabled={!(isNew ? s === "New" : ALLOWED[currentStatus]?.includes(s))}>{s}</option>)}
                        </select>
                        <small>Assigned dan Discarded hanya diisi otomatis oleh proses approval.</small>
                    </label>

                    <label>Asset Value (Total)<input value={rupiah(total)} readOnly /></label>

                    {!readOnly && <div className="full"><button className="primary" disabled={save.isPending}>{save.isPending ? "Menyimpan…" : "Simpan"}</button></div>}
                </form>
            )}

            {tab === "maint" && showMaintenance && detail.data && (
                <MaintenanceTab assetId={detail.data.id} assetCode={detail.data.assetCode} canEdit={!readOnly} onTotal={setTotal} />
            )}

            {!isNew && detail.data && <ApprovalCard assetId={detail.data.id} />}
        </>
    );
}