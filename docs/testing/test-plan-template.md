# Test Plan Template (Lightweight + Full)

Ten dokument jest wzorem do powtarzalnych testow przed releasem i po wiekszych zmianach.

## 1. Metadane

- Data:
- Osoba wykonujaca:
- Wersja/commit:
- Zakres zmian:
- Typ testu: `LIGHT` / `FULL`

---

## 2. Test LIGHT (szybki)

### 2.1 Kiedy uruchamiac

- Po zmianach w auth/session/startup/read-only.
- Przed testem prod-sim.

### 2.2 Czas

- 5-15 minut.

### 2.3 Kroki

1. Build backend:
   `dotnet build src/backend/Platform.Api/Platform.Api.csproj`
2. Testy automatyczne:
   `dotnet test src/backend/Platform.Tests/Platform.Tests.csproj`
3. (Opcjonalnie) build frontend:
   `cd src/frontend/electron && npm run build`
4. Start aplikacji dev:
   `scripts\\run-app.ps1`
5. Smoke manualny:
   - logowanie standardowe,
   - logowanie serwisowe,
   - podstawowa nawigacja,
   - zamkniecie aplikacji (backend powinien sie zamknac).

### 2.4 Kryterium PASS

- Build OK, testy OK, brak blockerow w smoke.

### 2.5 Wynik

- Status: `PASS` / `FAIL`
- Uwagi:

---

## 3. Test FULL (pelny)

### 3.1 Kiedy uruchamiac

- Przed oficjalnym wydaniem.
- Po wiekszych refaktorach.
- Po zmianach security i konfiguracji runtime.

### 3.2 Czas

- 60-180 minut.

### 3.3 Zakres automatyczny

1. Build backend:
   `dotnet build src/backend/Platform.Api/Platform.Api.csproj`
2. Build frontend:
   `cd src/frontend/electron && npm run build`
3. Testy backend:
   `dotnet test src/backend/Platform.Tests/Platform.Tests.csproj`
4. Publish:
   - backend: `scripts\\publish-backend-dev.ps1` (lub docelowy publish)
   - app: skrypt publish dla instalatora

### 3.4 Zakres manualny (regresja)

1. Startup i runtime:
   - dynamiczny port,
   - runtime.json lokalny i shared,
   - brak zawieszek startup.
2. Auth:
   - standard login/logout,
   - service login (localhost only),
   - lockout po blednych probach.
3. Security:
   - read-only blokuje zapisy poza whitelista,
   - logout-beacon dziala tylko dla poprawnego token/session_id,
   - brak sekretow w appsettings.
4. Tryb serwisowy:
   - wejscie/wyjscie,
   - zapis konfiguracji DB,
   - brak starych instancji po relaunch.
5. Stabilnosc:
   - zamkniecie aplikacji zamyka backend,
   - brak krytycznych wyjatkow w logach.

### 3.5 Kryterium PASS

- Brak blockerow/krytycznych defektow.
- Wszystkie testy automatyczne PASS.
- Scenariusze manualne krytyczne PASS.

### 3.6 Wynik

- Status: `PASS` / `FAIL`
- Lista defektow:
- Decyzja: `Release` / `No Release`

---

## 4. Rejestr wynikow (do wypelnienia)

| Obszar | Test | Wynik | Uwagi |
|---|---|---|---|
| Build | Backend | PASS/FAIL | |
| Build | Frontend | PASS/FAIL | |
| Tests | Platform.Tests | PASS/FAIL | |
| Auth | Login/logout | PASS/FAIL | |
| Auth | Service account | PASS/FAIL | |
| Security | Read-only | PASS/FAIL | |
| Security | Logout-beacon | PASS/FAIL | |
| Startup | Runtime/ports | PASS/FAIL | |
| Service mode | DB config flow | PASS/FAIL | |
| Stability | App close/shutdown | PASS/FAIL | |

---

## 5. Notatka koncowa

- Podsumowanie:
- Ryzyka pozostale:
- Rekomendacja:

