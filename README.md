# DevProjectsOrganizer

Windows-only project organizer with a fast .NET scanning engine and a web UI hosted in WebView2.

**Tech stack**
- Backend: .NET 10 (`src/Engine`)
- Desktop host: WPF + WebView2 (`src/AppHost`)
- Frontend: Angular + Angular Material (`ui/`)

**Repo layout**
- `src/Engine` — scanning engine, heuristics, data layer
- `src/AppHost` — WPF host that embeds the Angular UI
- `ui` — Angular app (Material UI)

**Prerequisites**
- Windows 10/11
- .NET SDK 10
- Node.js >= 20.19
- npm (bundled with Node)

**Run (development)**
1. Start Angular dev server:
   ```powershell
   cd ui
   npm install
   npm start
   ```
2. Run the WPF host:
   ```powershell
   dotnet run --project src/AppHost/AppHost.csproj
   ```
The app loads `http://localhost:4200/` by default in Debug.

**Run (production-like)**
1. Build Angular:
   ```powershell
   cd ui
   npm install
   npm run build
   ```
2. Run the host:
   ```powershell
   dotnet run --project src/AppHost/AppHost.csproj
   ```
In Release, AppHost tries to load `ui/dist/**/index.html`.

**Override UI URL**
You can point the host to a custom URL:
```powershell
$env:APPHOST_UI_URL = "http://localhost:4200/"
dotnet run --project src/AppHost/AppHost.csproj
```

**Notes**
- WebView2 uses the Edge (Chromium) runtime bundled with Windows.
- Angular Material theme is `indigo-pink` (see `ui/src/styles.scss`).
