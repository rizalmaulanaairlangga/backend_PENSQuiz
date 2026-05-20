# PENSQuiz Backend (ASP.NET Core)

An ASP.NET Core REST API serving as the backbone for PENSQuiz. Built with C#, Supabase Postgres/Auth (JWT), and Notion CMS integration. Powers quiz creation, student attempts, and automated answer scoring.

Backend API ini terpisah dari project frontend dan menggunakan:
*   **Database**: Supabase Postgres (schema di `sql/schema.sql` atau file eksternal)
*   **Auth**: Supabase Auth (JWT Validation)
*   **Asset/CMS Ringan**: Notion API integration (untuk manajemen modul/soal dinamis)

---

## 🛠️ Tech Stack & Prerequisites

*   **.NET SDK 8.0** atau yang terbaru
*   **Node.js & npm** (untuk menjalankan seeders di folder `seeders/`)
*   **Supabase Project** (Database & Auth JWT)
*   **Notion Integration** (Opsional, untuk CMS asset)

---

## 🚀 Langkah Instalasi & Setup

### 1) Setup Database (Supabase)
Jalankan file schema SQL yang disediakan (`sql/schema.sql` jika ada, atau script schema terkait) di SQL Editor Supabase Anda untuk membuat tabel-tabel yang diperlukan.

### 2) Konfigurasi Environment & API
Salin `.env.example` menjadi `.env` di root folder backend:
```bash
cp .env.example .env
```

Edit file `.env` tersebut atau konfigurasi langsung di `src/PensQuiz.Api/appsettings.json`.

**Parameter Wajib:**
*   `SUPABASE_CONNECTION_STRING` atau `ConnectionStrings:SupabasePostgres` — Connection string database Supabase Postgres Anda.
*   `SUPABASE_JWT_SECRET` atau `Supabase:JwtSecret` — JWT Secret Key dari project Supabase Anda (digunakan untuk validasi JWT token).

**Parameter Opsional (Validasi JWT & Notion CMS):**
*   `Supabase:JwtIssuer`
*   `Supabase:JwtAudience`
*   `NOTION_API_KEY` (Notion Integration Key)
*   `NOTION_*_DB_ID` (Database ID terkait di Notion)

### 3) Menjalankan Database Seeder (Node.js)
Jika Anda ingin mengisi database dengan data awal / data uji coba:
```bash
# Install dependencies seeder
npm install

# Menjalankan seeder
npm run db:seed

# Menghapus data seeder
npm run db:seed:clear
```

### 4) Menjalankan API Server (.NET)
Jalankan perintah berikut di terminal:
```powershell
dotnet run --project src/PensQuiz.Api
```
API akan berjalan di local environment. Jika berjalan dalam mode `Development` (`ASPNETCORE_ENVIRONMENT=Development`), dokumentasi **Swagger/OpenAPI UI** akan otomatis tersedia di browser.

---

## 🛣️ List Endpoint API Utama

| Method | Endpoint | Keterangan | Autentikasi (JWT) |
| :--- | :--- | :--- | :---: |
| **GET** | `/api/health` | Cek status kesehatan API | ❌ |
| **GET** | `/api/me` | Mengambil data profil user aktif |  |
| **PUT** | `/api/me/avatar` | Memperbarui URL foto profil user |  |
| **GET** | `/api/quizzes` | Mengambil seluruh daftar quiz |  |
| **GET** | `/api/quizzes/mine` | Mengambil quiz buatan user aktif |  |
| **POST** | `/api/quizzes` | Membuat quiz baru |  |
| **PUT** | `/api/quizzes/{id}` | Memperbarui data quiz (hanya pemilik) |  |
| **POST** | `/api/quizzes/{id}/copy` | Menyalin quiz publik orang lain |  |
| **POST** | `/api/quizzes/{quizId}/attempts/start` | Memulai sesi pengerjaan quiz baru |  |
| **POST** | `/api/attempts/{attemptId}/answers` | Menyimpan jawaban soal |  |
| **POST** | `/api/attempts/{attemptId}/submit` | Menyelesaikan & mengumpulkan hasil quiz |  |
