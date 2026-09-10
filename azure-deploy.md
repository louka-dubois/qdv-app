# Déploiement sur Azure App Service

Guide pour déployer QDVapp sur **Azure App Service** (conteneurs).

## Prérequis

- Un compte Azure (essai gratuit : https://azure.microsoft.com/free)
- Docker installé et démarré localement
- [Azure CLI](https://learn.microsoft.com/fr-fr/cli/azure/install-azure-cli)

> **Note port** : le `Dockerfile` écoute sur le port **8080**. Pour Azure App Service, ajouter l'app setting `WEBSITES_PORT=8080`.

## 1. Authentification Azure

```bash
az login
```

## 2. Créer les ressources

```bash
# Variables à adapter (noms uniques au niveau mondial)
RESOURCE_GROUP=qdvapp-rg
LOCATION=westeurope
APP_NAME=qdvapp
ACR_NAME=qdvappacr

# Groupe de ressources
az group create --name $RESOURCE_GROUP --location $LOCATION

# Azure Container Registry (pour stocker l'image)
az acr create --resource-group $RESOURCE_GROUP --name $ACR_NAME --sku Basic

# Plan App Service Linux (B1 = payant, ~27€/mois ; il existe un free tier)
az appservice plan create --resource-group $RESOURCE_GROUP \
  --name qdvapp-plan --is-linux --sku B1
```

## 3. Construire et pousser l'image Docker

```bash
AZURE_ACR_URL=$ACR_NAME.azurecr.io

# Construire
docker build -t $AZURE_ACR_URL/qdvapp:latest .

# Connexion au registry
az acr login --name $ACR_NAME

# Pousser l'image
docker push $AZURE_ACR_URL/qdvapp:latest
```

## 4. Créer l'app service

```bash
az webapp create --resource-group $RESOURCE_GROUP \
  --plan qdvapp-plan \
  --name $APP_NAME \
  --deployment-container-image-name $AZURE_ACR_URL/qdvapp:latest

# Autoriser le login ACR (ne pas utiliser le "admin user" en production)
az webapp config container set --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --docker-registry-server-url https://$ACR_URL \
  --docker-registry-server-user $ACR_NAME \
  --docker-registry-server-password $(az acr credential show -n $ACR_NAME --query passwords[0].value -o tsv)

az webapp restart --resource-group $RESOURCE_GROUP --name $APP_NAME
```

## 5. Configurer les variables d'environnement

**Stockage SQLite** : le fichier `.db` est volatil dans un conteneur. Monter un volume persistant (Azure Files) pointé sur `/data` :

```bash
az webapp config storage-account add \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --custom-id persistent-share \
  --storage-type AzureFiles \
  --account-name <STORAGE_ACCOUNT> --share-name <SHARE_NAME> \
  --access-key <ACCESS_KEY> --mount-path /data

# Définir les variables d'environnement
az webapp config appsettings set --resource-group $RESOURCE_GROUP --name $APP_NAME --settings \
  ConnectionStrings__DefaultConnection="Data Source=/data/qdvapp.db" \
  Smtp__Host="smtp.gmail.com" \
  Smtp__Port="587" \
  Smtp__FromEmail="duboisbegl@gmail.com" \
  Smtp__FromName="QDVapp Vérifications" \
  Smtp__Username="<GOOGLE_APP_PASSWORD_USER>" \
  Smtp__Password="<GOOGLE_APP_PASSWORD>" \
  ASPNETCORE_ENVIRONMENT="Production"
```

> **SMTP Gmail** : utilisez un [mot de passe d'application](https://myaccount.google.com/apppasswords) (nécessite 2FA activé), pas votre mot de passe Gmail normal.

## 6. Activer HTTPS

Azure App Service fournit HTTPS automatiquement avec un certificat `*.azurewebsites.net` géré.

## 7. Mettre à jour après un changement de code

```bash
docker build -t $AZURE_ACR_URL/qdvapp:latest .
az acr login --name $ACR_NAME && docker push $AZURE_ACR_URL/qdvapp:latest
az webapp restart --resource-group $RESOURCE_GROUP --name $APP_NAME
```

## Alternative plus simple : CD continue via GitHub

La méthode exacte donnera la même chose, se fait en 4 clics dans le portail :
Azure Portal → votre Web App → *Deployment Center* → *Container Registry* → sélectionner `qdvapp/latest`.
À chaque `git push` sur `master`, GitHub Actions reconstruit et redéploie automatiquement.

## Verdict

| Élément | Valeur |
|---|---|
| URL du site | `https://qdvapp.azurewebsites.net` |
| Base de données | SQLite sur Azure Files (ou SQL Server pour un vrai prod) |
| Coût | ~0 € (free tier) à ~30 €/mois (B1) |