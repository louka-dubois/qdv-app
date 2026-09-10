# Déploiement sur Render (gratuit, sans carte bancaire)

Guide pour déployer QDVapp sur **Render.com** — aucune carte bancaire requise.

## 1. Pré-requis

- Un compte GitHub avec le repo `qdv-app` poussé.
- Un compte https://render.com (inscription via GitHub, 1 clic).

## 2. Déployer le site (2 façons)

### Option A : Blueprint (recommandé) — déploiement automatique à chaque `git push`

Le fichier `render.yaml` est déjà dans le repo. Dans le dashboard Render :

1. **New + → Blueprint**
2. Connecter le repo GitHub `qdv-app`
3. Render lit `render.yaml` et propose de créer le service `qdvapp`
4. Au moment de la création, il demandera les valeurs des variables `sync: false` :
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
| `ConnectionStrings__DefaultConnection` | `Data Source=qdvapp.db` |
| `Smtp__Host` | `smtp.gmail.com` |
| `Smtp__Port` | `587` |
| `Smtp__FromEmail` | `duboisbegl@gmail.com` |
| `Smtp__FromName` | `QDVapp Vérifications` |
| `Smtp__Username` | ton e-mail Gmail |
| `Smtp__Password` | voir ci-dessous |

> **SMTP Gmail** : génère un [mot de passe d'application](https://myaccount.google.com/apppasswords) (active d'abord la double authentification), puis utilise-le comme `Smtp__Password` — et non ton mot de passe Gmail normal.

## 4. Ce que tu obtiens

- **URL** : `https://qdvapp.onrender.com` (HTTPS automatique)
- Re-déploiement auto à chaque `git push`
- **Gratuit** si tu restes sous 750 h/heures de service par mois

## Limites du plan gratuit (important)

- Le site **s'endort après 15 min d'inactivité** et met ~30–60 s à se réveiller au prochain clic. Normal, pas un bug.
- Le **fichier SQLite est éphémère** : chaque re-déploiement réinitialise la base (comptes, données Excel). Acceptable pour une démo / soutenance.
  - Pour persister les données plus tard : monter un disque (plan payant) ou passer sur Postgres externe gratuit (Neon/Supabase).
- Les données utilisées par `wwwroot/files` sont aussi volatiles.

## Débogage

- Logs : Dashboard → service `qdvapp` → **Logs**
- Relancer manuellement : Dashboard → service → **Manual Deploy** → `Clear build cache & deploy`