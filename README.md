# Greenfall: Relik Terakhir

Dokumen ringkas ini menjadi Game Design Document (GDD) dan panduan menjalankan project Unity.

## Identitas tugas

| Kolom | Isi |
|---|---|
| Nama mahasiswa | Adin Prasetya |
| NIM | 14012300049 |
| Kelas | 7A-Sen |
| Mata kuliah | Game Development |
| Dosen pengampu | Ali Rohman, S.Kom., M.Kom. |
| Materi | UTS |

## 1. Identitas dan konsep game

- **Judul:** Greenfall: Relik Terakhir
- **Genre:** 3D action-adventure, eksplorasi dan pertarungan jarak dekat.
- **Platform/perspektif:** PC Windows, third-person.
- **Target pemain:** Remaja 13+; aksi fantasi tanpa konten grafis.
- **Latar ide:** Game memadukan eksplorasi lingkungan alam 3D dengan pertarungan sederhana. Pemain perlu bergerak di medan terbuka, mengatur jarak serang, menghindari penjaga, dan menyelesaikan tujuan sebelum menang.
- **Sinopsis:** Cahaya hutan Greenfall mulai padam setelah relik kuno tersebar. Pemain berperan sebagai penjelajah yang harus menemukan lima relik dan mengalahkan tiga penjaga untuk memulihkan hutan. Jika kesehatan habis, misi gagal.

## 2. Mekanik gameplay dan level

### Kontrol

| Input | Aksi |
|---|---|
| W, A, S, D | Bergerak relatif terhadap arah kamera |
| Mouse | Memutar kamera third-person |
| Shift kiri | Berlari |
| Space | Melompat |
| Klik kiri | Menyerang penjaga di depan |
| Esc | Jeda / lanjutkan |

### Aturan dan kondisi permainan

- Pemain mulai dengan 100 kesehatan; setiap serangan musuh yang mengenai pemain mengurangi 10.
- Serangan pemain mengurangi 25 kesehatan musuh dan memiliki jeda serang.
- Tujuan menang adalah mengumpulkan **relik** dan mengalahkan **penjaga**.
- Pemain memiliki batas waktu. Waktu habis atau kesehatan mencapai nol berarti kalah.
- Skor bertambah **100 poin** per relik dan **250 poin** per penjaga yang dikalahkan.
- HUD menampilkan kesehatan, waktu tersisa, skor, jumlah relik, dan jumlah penjaga yang dikalahkan. Tersedia menu awal, pause, layar menang/kalah, dan mulai ulang.
- Alur awal menampilkan loading scene, layar judul dengan tombol **Mulai**, lalu menu berisi lanjutkan game, pilih level, pengaturan, dan kredit. Level yang tersedia saat ini adalah Hutan Greenfall; loading menampilkan progres pemuatan scene.

### Fisika dan lingkungan 3D

- `CharacterController` menangani gerak, gravitasi, dan lompatan pemain.
- `CharacterController` pemain dan collider penjaga menangani tabrakan; sphere collider trigger pada relik menangani pengumpulan, yang hanya dipicu oleh pemain.
- Musuh memakai `NavMeshAgent` bila berada di NavMesh, dan bergerak langsung di terrain sebagai fallback bila NavMesh belum dibake.
- Level Greenfall menggunakan terrain, pepohonan/vegetasi, batu, skybox, directional light, fog, dan efek URP.
- Seluruh tujuan berada di scene `Greenfall`; level tidak memiliki perpindahan scene.

## 3. Spesifikasi teknis dan aset

- **Engine:** Unity 6.6.5f1 (`6000.6.5f1`), Universal Render Pipeline 17.6.
- **Rendering:** PC menggunakan directional light utama beserta bayangannya; fitur additional light, cookie, dan light layer yang tidak digunakan dinonaktifkan untuk mengurangi varian shader.
- **Bahasa:** C#.
- **Script inti:**
  - `PlayerCharacterController.cs` — gerak, lompatan, kesehatan, dan serangan.
  - `CharacterFollowCamera.cs` — kamera third-person.
  - `EnemyAI.cs`, `EnemyCombat.cs`, `EnemyHealth.cs` — pengejaran, serangan, dan kesehatan penjaga.
  - `RelicPickup.cs` — animasi ringan dan trigger koleksi relik.
  - `GameManager.cs` — keadaan permainan, HUD, menu, objektif, dan audio prosedural.
  - `InstallAdventureGame.cs` — pemasangan konten permainan ke scene editor.
- **Model dan animasi:** Model pemain `Aj.fbx`, model penjaga `Big Vegas.fbx`, dan animasi yang tersedia di `Assets/Character`, dirangkai melalui `Animator Controller.controller`. Project juga menggunakan aset terrain yang sudah berada di dalam project. Sumber/persyaratan lisensi aset perlu dicocokkan dengan paket asal sebelum pengumpulan.
- **Vegetasi:** Pohon SpeedTree mempertahankan animasi angin dengan kualitas `Better` yang seragam untuk mengurangi varian shader yang perlu dikompilasi.
- **Audio/UI:** Musik latar prosedural, bunyi relik dan penjaga, menu utama, HUD, pause, dan layar hasil dibangun saat runtime.

## 4. Dokumentasi hasil dan pengumpulan

Ambil tangkapan layar setelah menjalankan Play Mode, lalu masukkan gambar asli pada empat posisi berikut:

1. **Menu utama:** layar judul Greenfall dan kontrol.
2. **Gameplay/lingkungan:** pemain di terrain dengan HUD.
3. **Mekanik:** pengambilan relik atau pertarungan dengan penjaga.
4. **Unity Editor:** scene dan Hierarchy yang menunjukkan pemain, Game Manager, relik, dan penjaga.