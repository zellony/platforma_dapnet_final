import React, { useState, useEffect } from "react";
import { apiFetch, getErrorMessage } from "../../apiFetch";
import iconImg from "../../assets/icon.png";

export function LicenseActivationScreen({ onActivated }: { onActivated: () => void }) {
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [companyNip, setCompanyNip] = useState<string>("");
    const [companyName, setCompanyName] = useState<string>("");

    // ? AUTOMATYCZNE CZYSZCZENIE B£ÊDÓW
    useEffect(() => {
        if (error) {
            const timer = setTimeout(() => setError(null), 5000);
            return () => clearTimeout(timer);
        }
    }, [error]);

    useEffect(() => {
        apiFetch("/company/public-info")
            .then(r => {
                if (r.ok) return r.json();
                throw new Error("Brak danych firmy");
            })
            .then(data => {
                setCompanyNip(data.nip);
                setCompanyName(data.fullName);
            })
            .catch(() => {
                console.warn("Nie uda³o siê pobraæ danych firmy do aktywacji.");
            });
    }, []);

    const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (!file) return;

        setBusy(true);
        setError(null);

        const reader = new FileReader();
        reader.onload = async (ev) => {
            const content = ev.target?.result as string;
            try {
                const res = await apiFetch("/license/upload", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ licenseContent: content })
                });

                if (res.ok) {
                    onActivated();
                } else {
                    // ? POPRAWKA: Próbujemy pobraæ szczegó³owy komunikat b³êdu
                    const msg = await getErrorMessage(res);
                    setError(msg);
                }
            } catch (ex) {
                setError("B³¹d po³¹czenia z serwerem.");
            } finally {
                setBusy(false);
                // Resetujemy input, aby mo¿na by³o wybraæ ten sam plik ponownie
                e.target.value = "";
            }
        };
        reader.readAsText(file);
    };

    const handleQuit = () => {
        if ((window as any).electron) {
            (window as any).electron.quitApp();
        }
    };

    return (
        <div className="setup-container">
            <div className="setup-box" style={{ width: "500px", textAlign: "center" }}>
                <div className="setup-header">
                    <div style={{ width: "64px", height: "64px", background: "rgba(86, 204, 242, 0.05)", borderRadius: "16px", display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 16px", border: "1px solid rgba(86, 204, 242, 0.1)" }}>
                        <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="var(--accent)" strokeWidth="2"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
                    </div>
                    <h2 className="setup-title">WYMAGANA AKTYWACJA</h2>
                    <div className="setup-subtitle">SYSTEM NIE POSIADA AKTYWNEJ LICENCJI</div>
                </div>

                {companyNip && (
                    <div style={{ marginBottom: 24, padding: "16px", background: "rgba(0,0,0,0.2)", borderRadius: "8px", border: "1px solid var(--border)" }}>
                        <div style={{ fontSize: "10px", color: "var(--text-muted)", marginBottom: 6, letterSpacing: "1px", fontWeight: 700 }}>DANE DO LICENCJI:</div>
                        <div style={{ fontSize: "18px", fontWeight: 700, color: "var(--accent)", letterSpacing: "1px", marginBottom: 4 }}>{companyNip}</div>
                        <div style={{ fontSize: "12px", color: "var(--text-main)", opacity: 0.8 }}>{companyName}</div>
                    </div>
                )}

                {error && (
                    <div style={{ 
                        marginBottom: 24, padding: "12px", borderRadius: 8, 
                        background: "rgba(235, 87, 87, 0.05)", 
                        border: "1px solid rgba(235, 87, 87, 0.2)", 
                        color: "var(--danger)", fontSize: "12px", fontWeight: 600,
                        lineHeight: "1.4", wordBreak: "break-word"
                    }}>
                        ? {error}
                    </div>
                )}

                <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                    <label className="primary" style={{ 
                        display: "flex", alignItems: "center", justifyContent: "center", gap: 12, padding: "14px", 
                        borderRadius: "8px", cursor: busy ? "wait" : "pointer", fontSize: "13px", fontWeight: 700,
                        transition: "all 0.2s ease", width: "100%"
                    }}>
                        <input type="file" accept=".lic" onChange={handleFileUpload} disabled={busy} style={{ display: "none" }} />
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>
                        {busy ? "PRZETWARZANIE..." : "WGRAJ PLIK LICENCJI (.LIC)"}
                    </label>

                    <button 
                        className="secondary danger" 
                        onClick={handleQuit} 
                        style={{ border: "none", background: "transparent", fontSize: "10px", opacity: 0.6, marginTop: 8 }}
                    >
                        ZAMKNIJ PROGRAM
                    </button>
                </div>

                <div style={{ marginTop: 32, fontSize: "10px", color: "var(--text-muted)", lineHeight: "1.5" }}>
                    Jeœli nie posiadasz pliku licencji, skontaktuj siê z dzia³em wsparcia DAPNET.<br/>
                    Pamiêtaj, ¿e licencja jest przypisana do numeru NIP Twojej firmy.
                </div>
            </div>
        </div>
    );
}
