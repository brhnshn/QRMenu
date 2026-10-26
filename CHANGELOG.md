# Changelog

All notable changes to this project will be documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [1.0.1] - 2026-10-26

### Security
- Removed `/Auth/Recovery` anonymous admin creation vulnerability and introduced secure CLI-based `--create-admin` flag.
- Implemented cryptographic token-based QR session mechanism (`Masa.QrToken`, `/qr/t/{token}`), eliminating sequential table ID enumeration attacks.
- Isolated `MenuHub` SignalR table groups and staff channels with strict cookie and role-based authorization checks.
- Defined IP and token-partitioned rate limiting policies for authentication and QR endpoints.
- Enforced strict CSP (`frame-ancestors 'self'`, `connect-src 'self' wss: https://translate.googleapis.com`) and cookie attributes (`SameSite=Lax`, `HttpOnly`, `Secure`).
- Bound PostgreSQL connection string to environment variables and restricted port `5432` to localhost (`127.0.0.1:5432`).
- Enforced magic-byte validation (JPEG, PNG, WEBP) for product image uploads.

### Fixed
- Prohibited setting payment statuses (`TamOdendi`, `KismiOdendi`, `Iade`) directly via `DurumGuncelleAsync`; restricted payment state transitions exclusively to `OdemeService`.
- Recalculated item unit prices on the server dynamically using live product, variation, and option prices during order placement, fixing Happy Hour window bypass.
- Prevented duplicate collections and race conditions in concurrent partial payments via atomic conditional updates.
- Aligned business day closing checks to Turkey standard time (`Europe/Istanbul`) and payment event timestamps; prohibited closing business days with active open orders.
- Ensured consistent stock returns upon item cancellations and prevented double stock returns during order cancellations.
- Enforced role-based target status whitelisting for kitchen and waiter updates (Kitchen: Preparing/Ready; Waiter: Delivered/Cancelled).
- Computed fortune wheel and game outcomes server-side using cryptographic RNG; blocked applying game discounts to partially or fully settled orders.

### Changed
- Modularized 2,300+ line monolithic `AdminController` into 5 domain-focused controllers (`AdminUrunController`, `AdminMasaController`, `AdminKullaniciController`, `AdminRaporController`, `AdminHappyHourController`) backed by dedicated Core/Data service abstractions.
- Externalized massive inline `<script>` blocks from Kitchen, Cashier, Admin, Menu, and Order views into modular JS files under `wwwroot/js/pages/` using a JSON Island and data-attribute architecture.
- Upgraded EF Core, Identity, and Npgsql dependencies to the latest 8.0.x patch releases.

### Added
- Added multi-stage build `Dockerfile` running under non-root `USER app` with an integrated `/health` healthcheck probe.
- Added `docker-compose.yml` and `.dockerignore` uniting PostgreSQL 16 and ASP.NET Core with isolated bridge networking (`qrmenu_network`), persistent volumes, and health monitoring.
- Added GitHub Actions CI workflow (`.github/workflows/ci.yml`) and Dependabot configuration (`.github/dependabot.yml`).
- Added `QRMenu.IntegrationTests` test suite with 41 end-to-end authorization, rate limiting, and security header tests, bringing total automated test coverage to 147 passing tests.
