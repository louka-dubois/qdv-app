# Déploiement sur Render (gratuit, sans carte bancaire)

Guide pour déployer QDVapp sur **Render.com** — aucune carte bancaire requise.

## Structure des données (depuis la refonte « per-user »)

- **Base de données : PostgreSQL** hébergée sur **Neon** (https://neon.tech, niveau gratuit, **sans carte bancaire**, IPv4). Toute la persistance vit là :
  comptes, corrections, projets/OF manuels et **les fichiers Excel uploadés** (stockés en BLOB).
- Chaque utilisateur (et chaque « invité ») a un **espace de travail isolé**.
- L'option gratuite de Render n'a plus d'impact sur les données : rien n'est stocké dans le conteneur.

## 1. Pré-requis

- Un compte GitHub avec le repo `qdv-app` poussé.
- Un compte https://render.com (inscription via GitHub, 1 clic).
- Une base **Neon** (https://neon.tech → Sign in with GitHub → **Create project** → plan *Free*, sans carte bancaire).

> **Pourquoi Neon et plus Supabase ?** Le plan gratuit de Supabase est désormais IPv6-only (l'IPv4 est payant) : le Mac de développement et Render ne peuvent pas l'atteindre. Neon fournit une adresse IPv4 sur le plan gratuit.

### 0. Créer la base Neon

1. **Create project** → nom (ex. `qdvapp`) → région proche (ex. `US East (Ohio)` alias *us-east-2*).
2. **Connect** → copier la chaîne **Pooled connection string**, de la forme :
   `postgresql://<user>:<password>@ep-<id>-pooler.us-east-2.aws.neon.tech/neondb?sslmode=require`
   - Pour .NET/Npgsql, l'équivalent est :
     `Host=ep-<id>-pooler.us-east-2.aws.neon.tech;Port=5432;Database=neondb;Username=<user>;Password=<password>;SSL Mode=Require`

## 2. Déployer le site (2 façons)

### Option A : Blueprint (recommandé) — déploiement automatique à chaque `git push`

Le fichier `render.yaml` est déjà dans le repo. Dans le dashboard Render :

1. **New + → Blueprint**
2. Connecter le repo GitHub `qdv-app`
3. Render lit `render.yaml` et propose de créer le service `qdvapp`
4. Au moment de la création, il demandera les valeurs des variables `sync: false` :
   - `ConnectionStrings__DefaultConnection` (ta chaîne Neon)
   - `Smtp__Username`
   - `Smtp__Password`
5. **Apply Blueprint** → Render build l'image et démarre le site

À chaque `git push` sur `master`, Redéploiement automatique.

### Option B : Manuel

1. **New + → Web Service** → connecter le repo GitHub `qdv-app`
2. Configurer :
   - **Runtime** : `Docker`
   - **Region** : `Frankfurt (EU Central)` (le plus proche)
   - **Plan** : `Free`
3. Ajouter les variables d'environnement (voir tableau ci-dessous)
4. **Deploy Web Service**

## 3. Variables d'environnement

| Clé | Valeur |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | ta chaîne Neon (connexion `Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require`) |
| `Smtp__Host` | `smtp.gmail.com` |
| `Smtp__Port` | `587` |
| `Smtp__FromEmail` | `duboisbegl@gmail.com` |
| `Smtp__FromName` | `QDVapp Vérifications` |
| `Smtp__Username` | ton e-mail Gmail |
| `Smtp__Password` | voir ci-dessous |

> **SMTP Gmail** : génère un [mot de passe d'application](https://myaccount.google.com/apppasswords) (active d'abord la double authentification), puis utilise-le comme `Smtp__Password` — et non ton mot de passe Gmail normal.

> **Migrations** : à chaque démarrage, l'application applique automatiquement les migrations EF Core (`db.Database.Migrate()`), donc le schéma se crée tout seul au premier lancement.

## 4. Ce que tu obtiens

- **URL** : `https://qdvapp.onrender.com` (HTTPS automatique)
- Re-déploiement auto à chaque `git push`
- **Gratuit** si tu restes sous 750 h/heures de service par mois
- Données **persistantes** : comptes, Excel uploadés, corrections et manuels survivent aux re-déploiements.

## Limites du plan gratuit (important)

- Le site **s'endort après 15 min d'inactivité** et met ~30–60 s à se réveiller au prochain clic. Normal, pas un bug.
- Chaque « invité » a son propre espace vide ; pour voir des données, il faut uploader un Excel (ou créer un compte).

## Débogage

- Logs : Dashboard → service `qdvapp` → **Logs**
- Relancer manuellement : Dashboard → service → **Manual Deploy** → `Clear build cache & deploy`