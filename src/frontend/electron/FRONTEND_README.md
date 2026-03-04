# Dokumentacja Frontendu - Platforma DAPNET

## ?? Stos Technologiczny
- **Framework:** React 18+ (TypeScript)
- **Budowanie:** Vite
- **Œrodowisko:** Electron (Aplikacja Desktopowa)
- **Zarz¹dzanie Stanem:** React Hooks (Context API dla Auth i Toastów)
- **Drag & Drop:** `@dnd-kit` (u¿ywany w Sidebarze oraz w kolumnach tabel)
- **Komunikacja:** Customowy `apiFetch` z obs³ug¹ HTTPS i HttpOnly Cookies

## ?? Struktura Projektu (Mapa Plików)

### `/src/frontend/app/` (G³ówny kod React)
- **`main.tsx`**: G³ówny punkt wejœcia. Zarz¹dza routingiem, timerami sesji oraz równoleg³ym ³adowaniem statusu systemu i firmy.
- **`apiFetch.ts`**: Centralna logika komunikacji. Obs³uguje szyfrowanie hase³ RSA, przechwytywanie logów dla dewelopera oraz bezpieczne przesy³anie ciasteczek (`credentials: 'include'`).
- **`menu.ts`**: Definicja struktury menu g³ównego.
- **`app.css`**: Globalne style i animacje.

### `/src/frontend/app/layout/` (Szkielet aplikacji)
- **`Shell.tsx`**: Globalny kontener. Zawiera TopBar z dynamiczn¹ ikon¹ **Developer Control Center** (tylko dla SuperAdmina) oraz system powiadomieñ Toast.
- **`SidebarLayout.tsx`**: Zarz¹dza podzia³em ekranu i warstwami interfejsu.
- **`Sidebar.tsx`**: Pasek boczny z obs³ug¹ DND i przyciskiem zamkniêcia aplikacji przez IPC.
- **`ReadyScreen.tsx`**: Zarz¹dza oknami, globalnymi skrótami klawiszowymi oraz dynamiczn¹ widocznoœci¹ œcie¿ek plików `.tsx`.

### `/src/frontend/app/components/` (Komponenty wielokrotnego u¿ytku)
- **`WindowContainer.tsx`**: Kontener dla okien modu³ów z obs³ug¹ Debug Info.
- **`DebugHud.tsx`**: Pó³przezroczysty pasek statystyk (RAM, Ping, Okna) wyœwietlany na górze ekranu.
- **`WindowSearch.tsx`**: Szybkie wyszukiwanie okien (`Ctrl + Spacja`).
- **`DbSessionsPanel.tsx`**: Podgl¹d aktywnych sesji bazy danych.

### `/src/frontend/app/features/` (Widoki modu³ów)
- **`/Admin/`**:
    - `DevToolsView.tsx`: Centrum dowodzenia dewelopera (prze³¹czniki HUD, Logger, Emulator).
    - `ApiLoggerView.tsx`: Szczegó³owa historia zapytañ API z podgl¹dem Body.
    - `PermissionEmulatorView.tsx`: Narzêdzie do testowania uprawnieñ "w locie".
    - `UsersView.tsx`, `RolesView.tsx`, `SystemInfoView.tsx`, itp.
- **`/Setup/`**: Ekrany konfiguracji bazy, firmy, licencji i konta administratora.

## ??? Architektura i Funkcjonalnoœci

### 1. Zarz¹dzanie Oknami (Workspace)
- **Focus & Auto-Sizing:** Inteligentne zarz¹dzanie rozmiarem i aktywnoœci¹ okien.
- **Debug Info:** Dynamiczne wyœwietlanie œcie¿ek plików w nag³ówkach (sterowane z DevTools).

### 2. System Nawigacji (Sidebar)
- **Personalizacja:** Kolejnoœæ elementów zapisywana w `localStorage`.
- **Zamykanie:** Przycisk zamkniêcia aplikacji zintegrowany z procesem g³ównym Electrona.

## ?? Skróty Klawiszowe
- `Ctrl + Shift + I`: Otwarcie DevTools (aktywne tylko dla SuperAdmina na produkcji).
- `Ctrl + Alt + H`: Prze³¹czenie widocznoœci Debug HUD.
- `Ctrl + Alt + F`: Prze³¹czenie widocznoœci œcie¿ek plików w nag³ówkach.
- `Ctrl + Spacja`: Wyszukiwanie okna.
- `Ctrl + ~`: Zwiniêcie/Rozwiniêcie Sidebaru.

## ?? Bezpieczeñstwo (Wdro¿one w 2025)
- **HTTPS (TLS):** Ca³a komunikacja z lokalnym API jest szyfrowana certyfikatem SSL generowanym automatycznie.
- **RSA Encryption:** Has³a u¿ytkowników s¹ szyfrowane kluczem publicznym na frontendzie przed wysy³k¹ do API.
- **HttpOnly Cookies:** Tokeny sesji (JWT) s¹ przechowywane w bezpiecznych ciasteczkach, niedostêpnych dla JavaScriptu (ochrona przed XSS).
- **RAM Hardening:** Automatyczne zerowanie pól hase³ natychmiast po klikniêciu przycisku logowania.
- **SafeStorage:** Dane wra¿liwe na dysku (np. zapamiêtany login) s¹ szyfrowane kluczem sprzêtowym Windows (DPAPI).
- **Content Protection:** Blokada robienia zrzutów ekranu i nagrywania okna aplikacji (aktywna w wersji produkcyjnej).

## ??? Narzêdzia Deweloperskie (Developer Control Center)
Dostêpne tylko dla u¿ytkowników z flag¹ `isSystemAdmin` lub loginem `AdminDAPNET`:
- **Debug HUD:** Monitorowanie zu¿ycia RAM przez proces Electrona oraz opóŸnieñ API w czasie rzeczywistym.
- **API Request Logger:** Pe³na historia komunikacji z serwerem wraz z podgl¹dem przesy³anych danych JSON.
- **Permission Emulator:** Mo¿liwoœæ tymczasowej zmiany uprawnieñ w celu przetestowania widocznoœci elementów UI.
- **Diagnostic Export:** Funkcja generowania kompletnego raportu technicznego do pliku `.txt` na pulpicie.

## ?? Wygl¹d (UI/UX)
- **Motyw:** Dark Technical Theme (Cyberpunk/Industrial).
- **UX:** System "Click Outside" dla menu u¿ytkownika, ujednolicone animacje hover i spójna kolorystyka akcentów (`var(--accent)`).

---

## ?? Ostatnie Zmiany (Luty 2025)

### 1. Architektura LEGO (Modu³owoœæ UI)
- **Pe³na Izolacja:** G³ówne elementy interfejsu (TopBar, Sidebar, Pulpit, Taskbar) zosta³y wydzielone do osobnych plików `.tsx` i `.css`.
- **Stabilnoœæ:** Zmiana stylu w jednym "klocku" nie wp³ywa na uk³ad pozosta³ych.
- **Elastycznoœæ:** Sidebar i Workspace zajmuj¹ teraz 100% wysokoœci pod TopBarem, a Taskbar przylega do Sidebaru z prawej strony.

### 2. Centralny System Ikon (`DapnetIcon`)
- **Unifikacja:** Wprowadzono jeden komponent obs³uguj¹cy zarówno ikony z biblioteki systemowej, jak i surowe kody SVG przesy³ane dynamicznie z modu³ów DLL.
- **Spójnoœæ:** Te same ikony s¹ teraz u¿ywane w Sidebarze, Taskbarze, na Pulpicie i w nag³ówkach okien.

### 3. Reanimacja Pulpitu
- **Nowy Wygl¹d:** Ikony otrzyma³y kafelkowy styl z efektem glassmorphism i neonowym podœwietleniem.
- **Siatka Pionowa:** Nowe skróty uk³adaj¹ siê automatycznie kolumnami (od góry do do³u).
- **P³ynnoœæ:** Przesuwanie ikon (Drag & Drop) zyska³o akceleracjê sprzêtow¹ (Hardware Acceleration).

### 4. Inteligentny Taskbar (Pasek Zadañ)
- **Zarz¹dzanie Oknami:** Dodano logikê minimalizacji, przywracania i aktywacji okien przez klikniêcie w kafelek.
- **Menu Kontekstowe:** Mo¿liwoœæ zamykania konkretnych modu³ów lub wszystkich modu³ów naraz (z wy³¹czeniem folderów).
- **Tryby:** Prze³¹cznik miêdzy widokiem "Ikona + Tekst" a "Tylko ikony".

### 5. Dynamiczny Panel Systemowy (Drawer)
- **Bottom-Up Popup:** Panel informacji o systemie wysuwa siê teraz z do³u, idealnie wyrównany do Taskbaru.
- **Multi-Panel:** Dodano obs³ugê wysuwanych paneli bocznych (np. szczegó³owa lista modu³ów DLL).
- **Click-Outside:** Automatyczne zamykanie panelu po klikniêciu poza jego obszar.

### 6. Standaryzacja Widoków
- **Elastyczne Okna:** Wprowadzono klasy `.dapnet-view`, które wymuszaj¹ poprawne skalowanie treœci i inteligentne paski przewijania wewn¹trz okien modu³ów.
