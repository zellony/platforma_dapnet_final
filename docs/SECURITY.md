## Platforma DAPNET – Zabezpieczenia (stan: 2026-03-03)

### TLS i certyfikat
- Backend generuje samopodpisany cert (CN=localhost, SAN: localhost, 127.0.0.1) w `%ProgramData%/PlatformaDapnet/localhost.pfx`.
- Cert odnawia się automatycznie, gdy do wygaśnięcia zostaje <30 dni lub plik jest uszkodzony.
- Electron akceptuje tylko cert dla `localhost/127.0.0.1`; połączenia na inne hosty są blokowane. Nie wymaga zaufanego CA.

### Uruchamianie backendu
- Electron startuje backend jako proces potomny i łączy się po HTTPS na 127.0.0.1:5001.

### Uwierzytelnianie i sesje
- Hasła szyfrowane RSA po stronie frontendu (`/system/rsa-key`), odszyfrowanie w backendzie.
- JWT w ciasteczku HttpOnly `dapnet_session` (`Secure`, `SameSite=None`, path `/`).
- Sesje użytkowników zapisane w DB; blokada podwójnego logowania (409) z opcją force.
- Flaga `is_read_only` w tokenie; UI pokazuje baner i blokuje modyfikacje.

### Uprawnienia i DevTools
- SuperAdmin bypass (claim `is_system_admin` lub login `AdminDAPNET`).
- DevTools, skróty (F12, Ctrl+Shift+I) i menu DevTools dostępne wyłącznie dla SuperAdmina; próby bez uprawnień są blokowane/zamykane.

### Logi i dane wrażliwe
- Logger API maskuje pola wrażliwe; `/auth/*` oznaczane jako `[REDACTED]`; JWT/cookie/hasła zamaskowane.
- Eksport logów z DevTools zawiera zredagowane dane; logi można czyścić z UI.
- Zapamiętany login szyfrowany DPAPI (SafeStorage).

### Izolacja frontu
- `contextIsolation: true`, `nodeIntegration: false`; preload wystawia tylko potrzebne IPC.

### Ryzyka środowiskowe
- Ochrona dotyczy wyłącznie lokalnej komunikacji; lokalne malware/root ma pełny dostęp.
- Połączenia zdalne wymagają zmiany polityki hosta lub użycia certyfikatu zaufanego dla docelowego FQDN.
