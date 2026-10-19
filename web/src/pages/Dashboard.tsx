import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api, type AssetRow, type Paged } from "../api";
import { useAuth } from "../auth";

const count = (qs: string) =>
    api<Paged<AssetRow>>(`/api/assets?pageSize=1${qs}`).then((r) => r.total);

export default function Dashboard() {
    const { me, can } = useAuth();
    const canAssets = can("assets", "view");
    const all = useQuery({ queryKey: ["dash", "all"], queryFn: () => count(""), enabled: canAssets });
    const review = useQuery({ queryKey: ["dash", "review"], queryFn: () => count("&status=UnderReview"), enabled: canAssets });
    const assigned = useQuery({ queryKey: ["dash", "assigned"], queryFn: () => count("&status=Assigned"), enabled: canAssets });

    return (
        <>
            <h1>Dashboard</h1>
            <p>Halo, {me?.name}.</p>
            {canAssets && (
                <div className="stats">
                    <Link to="/assets" className="card stat"><b>{all.data ?? "…"}</b><span>Asset terlihat</span></Link>
                    <Link to="/assets" className="card stat"><b>{review.data ?? "…"}</b><span>Under Review</span></Link>
                    <Link to="/assets" className="card stat"><b>{assigned.data ?? "…"}</b><span>Assigned</span></Link>
                </div>
            )}
        </>
    );
}