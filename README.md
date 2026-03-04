# Platforma Dapnet - Backend / Frontend / Modules

## Struktura

```text
/src
  /backend
    /Platform.Api
    /Platform.Contracts
  /modules
    /Platform.Module.Health
    /Platform.Module.Ksef
  /frontend
    /app
    /electron
/scripts
/artifacts
```

## Moduly (pluginy DLL)

Backend (`Platform.Api`) laduje moduly z katalogu:

```text
{AppContext.BaseDirectory}/modules
```

W dev, projekty modulow po buildzie kopiuja DLL do:

```text
/src/backend/Platform.Api/bin/{Configuration}/net8.0/modules
```

## Frontend DEV uruchamia backend

Electron w trybie DEV domyslnie szuka backendu w:

```text
<repoRoot>/artifacts/backend/Platform.Api.exe
```

Aby przygotowac backend do dev:

```powershell
./scripts/publish-backend-dev.ps1
```

Aby uruchomic frontend:

```powershell
cd src/frontend/electron
npm install
npm run dev
```

Opcjonalnie mozna nadpisac sciezke backendu przez zmienna srodowiskowa `BACKEND_EXE_PATH`.

## Publish (jeden katalog artefaktow)

Glowny skrypt:

```powershell
./scripts/publish-all.ps1
```

Wyniki:
- `artifacts/backend`
- `artifacts/frontend`
- `artifacts/installer`

Przyklady:

```powershell
# pelny publish release (self-contained backend + installer)
./scripts/publish-all.ps1

# backend framework-dependent
./scripts/publish-all.ps1 -FrameworkDependent

# bez instalatora (tylko build frontend + backend)
./scripts/publish-all.ps1 -SkipInstaller

```

## Skrypty komponentowe

Tylko backend:

```powershell
./scripts/publish-backend-only.ps1
```

Tylko frontend (build bez instalatora):

```powershell
./scripts/publish-frontend-only.ps1
```

Tylko frontend z instalatorem:

```powershell
./scripts/publish-frontend-only.ps1 -WithInstaller
```

Tylko tools:

```powershell
./scripts/publish-tools-only.ps1
```

`tools` jest celowo niezalezne i nie jest dolaczane do `publish-all.ps1`.
