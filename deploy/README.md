# Deploy (VPS): Docker + Nginx + Certbot

This project runs in **3 Docker containers**:

- PostgreSQL
- PhotoSite.Api
- PhotoSite.Web (Blazor Server)

HTTPS is terminated by **nginx on the VPS host** using **Certbot** (Let’s Encrypt).

## 1) DNS

Create `A` records pointing to the VPS:

- `maria-yakovleva.ru` → `83.217.202.160`
- `www.maria-yakovleva.ru` → `83.217.202.160` (optional but recommended)

## 2) VPS prerequisites

- Open ports: `80`, `443` (and `22` for SSH)
- Install Docker + docker compose plugin
- Install nginx + certbot

## 3) Run containers

On the VPS, copy the repo (git clone) and create an env file:

- Copy [`deploy/.env.example`](deploy/.env.example) to `.env` (near `docker-compose.yml`) or export env vars in your shell.
- Set strong values for `POSTGRES_PASSWORD` and `JWT_KEY`.

Start:

```bash
docker compose up -d --build
```

API will be available only on `127.0.0.1:5081`, Web on `127.0.0.1:5080`.

### First admin account (before removing `POST /api/Auth/register`)

The Blazor “admin” UI treats anyone who can obtain a valid JWT via **`POST /api/Auth/login`** as admin (`GET /api/Auth/me` must succeed). There is no separate role check in the web client.

**Create the user once** (Postman, curl, or similar):

- Method: `POST`
- URL (examples):
  - On the VPS: `http://127.0.0.1:5081/api/Auth/register`
  - Through nginx: `https://YOUR_DOMAIN/api/Auth/register`
- Headers: `Content-Type: application/json`
- Body (raw JSON):

```json
{
  "username": "your_admin_login",
  "password": "Use-a-long-strong-password"
}
```

Then verify login:

```bash
curl -sS -X POST http://127.0.0.1:5081/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"your_admin_login","password":"Use-a-long-strong-password"}'
```

You should get `{ "token": "..." }`. Sign in from `/login` (or your admin login page) in the browser; the SPA stores the token and treats you as admin.

**Important:** the repository removes `POST /api/Auth/register` after bootstrap so strangers cannot create accounts. Deploy workflow:

1. Deploy/run an API image **that still includes** `register` **or** create the user before updating.
2. Create the admin user (steps above).
3. Pull/rebuild the latest code **without** `register` and redeploy.

If you already deployed an image without `register` and the DB has **zero** users, temporarily redeploy an older build that had `register`, create the user, then redeploy the locked-down image—or restore from backup.

## 4) Nginx reverse proxy

Copy [`deploy/nginx/maria-yakovleva.ru.conf`](deploy/nginx/maria-yakovleva.ru.conf) to your nginx sites config, for example:

- `/etc/nginx/sites-available/maria-yakovleva.ru.conf`
- symlink into `/etc/nginx/sites-enabled/`

Create webroot folder for challenges:

```bash
sudo mkdir -p /var/www/certbot
sudo chown -R www-data:www-data /var/www/certbot
```

Reload nginx:

```bash
sudo nginx -t && sudo systemctl reload nginx
```

## 5) Issue certificates (Certbot)

Using nginx plugin (simplest):

```bash
sudo certbot --nginx -d maria-yakovleva.ru -d www.maria-yakovleva.ru
```

Certbot will update the `ssl_certificate*` lines automatically (or confirm they match your domain).

Test auto-renew:

```bash
sudo certbot renew --dry-run
```

## 6) What to verify

- `https://maria-yakovleva.ru/` loads the site
- `https://maria-yakovleva.ru/api/...` reaches the API
- Images from `https://maria-yakovleva.ru/Storage/...` load correctly

## Notes

- **413 Request Entity Too Large on `/api/...`:** nginx rejects bodies larger than its default (~1 MiB). The sample site sets `client_max_body_size 50m` on HTTPS (aligned with API multipart limits). After editing nginx, run `sudo nginx -t && sudo systemctl reload nginx`.
- API applies EF migrations automatically on startup in Production (see `PhotoSite.Api/Program.cs`).
- For Blazor Server, nginx must pass WebSockets for `/_blazor`. This is handled in the provided nginx config.
- **Images / covers in the browser:** the Web app uses **same-origin** URLs (`/api/...`, `/Storage/...`) when `Api:PublicUrl` is unset. Nginx must proxy `/api/` and `/Storage/` to the API container (the sample config does). Do not put the Docker hostname `photosite-api` in image URLs visible to browsers.
- **Local development** with API on another port (e.g. `https://localhost:7265`): set `Api:PublicUrl` to that base URL (see `appsettings.Development.json`) so `<img src>` still points at the API while server-side HttpClient uses `Api:BaseUrl`.
