import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth";

export default function Login() {
    const { login } = useAuth();
    const nav = useNavigate();
    const [u, setU] = useState("sarah");
    const [p, setP] = useState("Pass1234!");
    const [err, setErr] = useState("");
    const [busy, setBusy] = useState(false);

    const submit = async (e: FormEvent) => {
        e.preventDefault(); setErr(""); setBusy(true);
        try { await login(u, p); nav("/", { replace: true }); }
        catch (x) { setErr((x as Error).message); }
        finally { setBusy(false); }
    };

    return (
        <div className="login">
            <form className="card" onSubmit={submit}>
                <h2>Asset Management</h2>
                {err && <div className="error">{err}</div>}
                <label>Username<input value={u} onChange={(e) => setU(e.target.value)} autoFocus /></label>
                <label>Password<input type="password" value={p} onChange={(e) => setP(e.target.value)} /></label>
                <button className="primary" disabled={busy}>{busy ? "Masuk…" : "Masuk"}</button>
                {/* <small>Akun uji: sarah, mike, leo, jane, emma, bob (password sama: Passw0rd!)</small> */}
            </form>
        </div>
    );
}