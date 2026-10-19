# Asset Management System

Full-stack technical assessment: sistem manajemen aset dengan maintenance, approval workflow, permission dinamis, dan integrasi multi-protokol.

| Lapisan | Teknologi |
|---|---|
| Backend | ASP.NET Core 8 Web API, EF Core 8, ASP.NET Core Identity + JWT |
| Frontend | React + Vite + TypeScript, TanStack Query, Tiptap (rich text) |
| Database | SQL Server 2022 |
| Logging | Serilog → Elasticsearch → Kibana |
| Protokol API | REST, JSON-RPC 2.0, XML-RPC, SOAP (SoapCore), plus klien konsumsi untuk keempatnya |
| Test | xUnit + FluentAssertions + SQLite in-memory |
| Deploy | Docker Compose |

## 1. Menjalankan dengan Docker (disarankan, satu perintah)

Prasyarat: Docker Desktop (RAM minimal 4 GB untuk Docker karena ada SQL Server dan Elasticsearch).

```bash
docker compose up -d --build
```

| Layanan | URL |
|---|---|
| Web (React) | http://localhost:3000 |
| API + Swagger | http://localhost:5000/swagger |
| Kibana | http://localhost:5601 |
| SQL Server | `localhost,14333` (user `sa`, password ada di `docker-compose.yml`) |

Database dibuat otomatis: **migration dan seed dijalankan saat API start**, jadi tidak ada langkah SQL manual. Start pertama butuh sekitar 30-60 detik karena menunggu SQL Server siap.

Perintah berguna:

```bash
docker compose ps                      # status container
docker compose logs -f api             # log API
docker compose stop                    # hentikan tanpa menghapus data
docker compose down                    # hapus container, data tetap (volume)
docker compose down -v                 # reset total: hapus data DAN key enkripsi
```

> **Penting:** jangan menghapus volume `dpkeys` saja. Volume itu menyimpan key enkripsi kolom `AssetValueTotal`; jika hilang, nilai terenkripsi yang sudah ada tidak bisa didekripsi. Untuk reset, gunakan `down -v` (menghapus data dan key bersamaan).

### Kibana
Buka *Stack Management → Data Views → Create*, isi pattern `assetmgmt-logs-*` dengan timestamp `@timestamp`, lalu buka *Discover*. Cari misalnya `Asset AST` atau `Approval` untuk melihat log aksi bisnis.

## 2. Menjalankan lokal (tanpa Docker untuk API dan Web)

Prasyarat: .NET SDK 8, Node.js 20+, Docker (hanya untuk SQL Server).

```bash
# 1) Database
docker compose up -d sqlserver

# 2) Konfigurasi rahasia (user-secrets, tidak masuk Git)
cd src/AssetManagement.Api
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,14333;Database=AssetDb;User Id=sa;Password=dev12345!;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "dev-only-key-minimal-32-karakter-ganti-ya!!"
cd ../..

# 3) API (migration + seed otomatis saat start)
dotnet run --project src/AssetManagement.Api --launch-profile http
#    lihat port di Properties/launchSettings.json (mis. http://localhost:5007)

# 4) Frontend (terminal lain)
cd web
npm install
API_URL=http://localhost:5007 npm run dev      # PowerShell: $env:API_URL="http://localhost:5007"; npm run dev
#    buka http://localhost:5173
```

Migration manual (opsional, tidak wajib karena otomatis saat start):

```bash
dotnet ef database update -p src/AssetManagement.Infrastructure -s src/AssetManagement.Api
```

## 3. Menjalankan test

```bash
dotnet test
```

Cakupan: penomoran AST-001 dan seterusnya, perhitungan Asset Value saat maintenance ditambah/diubah/dihapus, aturan maintenance furniture, visibilitas record, permission delete (IT User ditolak, IT Manager diizinkan), workflow approval (dua approve → Assigned, satu reject → Discarded, approver salah ditolak), dan hak export (hanya HQ Manager).

## 4. Akun uji

Password semua akun: **`Pass1234!`**

| User | Divisi | Jabatan | Role |
|---|---|---|---|
| `sarah` | Headquarters | Manager | Manager, CanViewAllAssets, AssetManager, Admin |
| `mike` | Headquarters | User | User |
| `leo` | IT Department | Manager | Manager |
| `jane` | IT Department | User | User |
| `emma` | Facilities Department | Manager | Manager |
| `bob` | Facilities Department | User | User |

Skenario cepat:
- **sarah**: melihat semua aset, tombol *Export Excel*, menu *User Management*, approver sebagai Asset Manager.
- **leo**: IT Manager, akses penuh aset IT (termasuk hapus), approver sebagai Department Head divisi IT.
- **jane**: IT User, bisa membuat/mengubah aset IT, **tidak bisa menghapus**.
- **bob/emma**: hanya melihat aset buatannya sendiri.

## 5. Database

- **EF Core Migrations**: `src/AssetManagement.Infrastructure/Migrations/`.
- **Seed** (organisasi, divisi, 6 user, 5 role, 3 menu, matriks permission): `src/AssetManagement.Infrastructure/Data/Seeder.cs`, dijalankan otomatis dan idempoten.
- **Skrip SQL skema**: `db/schema.sql` (dihasilkan dari migration, idempoten). Untuk membuat ulang:
  ```bash
  dotnet ef migrations script --idempotent -p src/AssetManagement.Infrastructure -s src/AssetManagement.Api -o db/schema.sql
  ```
  Skrip ini hanya membuat skema. User dan password di-seed oleh aplikasi karena hash password dibuat oleh ASP.NET Identity.
- Kolom `Assets.AssetValueTotal` **terenkripsi** (ASP.NET Data Protection lewat EF value converter); isi di database berupa ciphertext (`CfDJ8...`).

## 6. Pengujian API

- **Swagger UI**: `/swagger` (tombol *Authorize*, tempel token dari `POST /api/auth/login` tanpa kata `Bearer`).
- **Postman**: impor `postman/AssetManagement.postman_collection.json`. Jalankan folder *1. Auth* dulu (token tersimpan otomatis), lalu folder lain. Ubah variabel `baseUrl` (Docker `http://localhost:5000`, `dotnet run` sesuai `launchSettings.json`).

| Protokol | Endpoint | Catatan |
|---|---|---|
| REST | `/api/*` | assets, maintenances, approvals, users, roles, permissions, lookups |
| JSON-RPC 2.0 | `POST /rpc/json` | method `asset.get`, `asset.list` |
| XML-RPC | `POST /rpc/xml` | method `asset.get`, `asset.list` (DTD diblokir, cegah XXE) |
| SOAP | `POST /soap/assets` | WSDL di `/soap/assets?wsdl`, operasi `GetAsset`, `ListAssets` |
| Konsumsi | `GET /api/integration-demo/{id}` | API memanggil dirinya sendiri lewat keempat protokol dan membandingkan hasilnya (`consistent: true`) |

`{id}` selalu **Id database** (kolom identity), bukan angka dari `AssetCode` (AST-xxx); keduanya tidak harus sama.

## 7. Pemetaan terhadap spesifikasi

| Spesifikasi | Implementasi |
|---|---|
| 2.2 Asset ID `AST-` + 3 digit | SQL `SEQUENCE` + unique index |
| 2.2 Subcategory dependen, status default New | Peta kategori dari API (`/api/lookups/categories`), divalidasi di server |
| 2.3 Tab dan modal Maintenance | `MaintenanceTab.tsx`, `MaintenancesController` |
| 13 Aturan UI | Subcategory Chair/Table/Cabinet menyembunyikan tab Maintenance; status Assigned mengunci Requester; status New mewajibkan Description. Semua juga divalidasi di server |
| 14 Recalculate Asset Value | `MaintenanceService`, satu transaksi + row lock; dijumlah di memori karena kolom terenkripsi |
| 15 Enkripsi kolom | `EncryptedDecimalConverter` |
| 16-17 Permission objek dan record | Matriks menu per role (policy `menu:aksi`) + filter record `AssetService.Visible/CanModify` |
| 18 Export Excel | Policy `assets:export` (HQ + Manager), ClosedXML |
| 19-22 Approval | `ApprovalService`, kartu Approval List |
| 23-25 User Management dan sidebar dinamis | `UsersController`, `RolesController`, `PermissionsController`, `GET /api/me/menu` |
| SOAP/XML-RPC/JSON-RPC/REST | Buat dan konsumsi, lihat bagian 6 |
| Logging | Serilog → Elasticsearch/Kibana |

## 8. Keputusan dan asumsi

**Akses**
- Dua lapis: matriks menu per role (boleh melakukan aksi?) dan aturan record (boleh terhadap baris ini?).
- **Lihat**: creator, staf IT untuk aset buatan IT, role `CanViewAllAssets`, dan approver aset tersebut. Record yang tidak terlihat dijawab 404 (bukan 403).
- **Ubah**: creator, atau staf IT untuk aset buatan IT. `CanViewAllAssets` hanya baca.
- **Hapus**: harus lolos matriks (role `User` tidak punya Delete) **dan** aturan record. Konsekuensinya user non-Manager tidak bisa menghapus aset buatannya sendiri.
- "Semua karyawan dapat membuat dan melihat" diartikan: boleh membuat, dan melihat record sesuai aturan poin 17.
- Sarah memegang `CanViewAllAssets`, `AssetManager`, dan `Admin` agar semua fitur bisa dicoba dengan satu akun.
- Perubahan matriks permission berlaku langsung. Perubahan role milik seorang user berlaku setelah user **login ulang** (`role_id` dibaca dari token JWT).

**Workflow**
- Department Head = manager di divisi requester. Asset Manager = user dengan role `AssetManager`.
- Status `Assigned` dan `Discarded` hanya bisa diisi workflow. Transisi manual yang diizinkan: New → UnderReview, Returned → UnderReview, Assigned → Returned.
- Requester yang juga manager divisinya dapat menyetujui permintaannya sendiri.

**Data dan keamanan**
- Asset ID memakai SQL sequence: nomor bisa loncat saat rollback, tidak pernah duplikat. Setelah AST-999 formatnya menjadi AST-1000.
- `AssetValueTotal` terenkripsi, sehingga tidak bisa di-`SUM`, di-`ORDER BY`, atau difilter di SQL. Nilainya selalu dihitung dari `AssetMaintenances.Cost`.
- Description (rich text) disanitasi (HtmlSanitizer) untuk mencegah XSS. Export Excel menetralkan formula injection.
- Token JWT disimpan di `localStorage` (sederhana; trade-off terhadap XSS).
- Password dan key di `docker-compose.yml` hanya untuk development.
- Tidak ada optimistic concurrency pada edit aset (last write wins).

## 9. Struktur repositori

```
AssetManagement/
├─ src/
│  ├─ AssetManagement.Domain/          entity dan enum
│  ├─ AssetManagement.Application/     DTO, validator, aturan bisnis, exception
│  ├─ AssetManagement.Infrastructure/  DbContext, migrations, seeder, service
│  └─ AssetManagement.Api/             controller, RPC/SOAP, klien konsumsi, Program.cs
├─ tests/AssetManagement.Tests/
├─ web/                                React app (+ Dockerfile, nginx.conf)
├─ db/schema.sql                       skrip skema (dari migration)
├─ postman/                            Postman collection
├─ docker-compose.yml
└─ README.md
```
