#!/usr/bin/env bash
set -euo pipefail

CURRENT_FOLDER=$(dirname "$0")

echo "Script to deploy the MCP Kusto Server as an Azure Container App"
echo ""
echo "Usage:  --subscrId <value> --envPrefix <value> --acrName <value> --tenantId <value> --clientId <value> --kustoClusterUrl <value> --kustoDatabaseName <value> [--location <value>] [--cpu <value>] [--memory <value>]"
echo ""

# Defaults
location="westeurope"
cpu="0.5"
memory="1.0Gi"
subscrId="" envPrefix="" acrName="" tenantId="" clientId="" kustoClusterUrl="" kustoDatabaseName=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --subscrId)          subscrId="$2"; shift 2 ;;
        --envPrefix)         envPrefix="$2"; shift 2 ;;
        --acrName)           acrName="$2"; shift 2 ;;
        --tenantId)          tenantId="$2"; shift 2 ;;
        --clientId)          clientId="$2"; shift 2 ;;
        --kustoClusterUrl)   kustoClusterUrl="$2"; shift 2 ;;
        --kustoDatabaseName) kustoDatabaseName="$2"; shift 2 ;;
        --location)          location="$2"; shift 2 ;;
        --cpu)               cpu="$2"; shift 2 ;;
        --memory)            memory="$2"; shift 2 ;;
        *) echo "Unknown argument: $1"; exit 1 ;;
    esac
done

if [[ -z "$subscrId" || -z "$envPrefix" || -z "$acrName" || -z "$tenantId" || -z "$clientId" || -z "$kustoClusterUrl" || -z "$kustoDatabaseName" ]]; then
    echo "ERROR: All required parameters must be provided."
    exit 1
fi

resGroup=$envPrefix-rg
containerAppsEnv=$envPrefix-env
appName=$envPrefix-server
imageName=mcp-kusto-server

az account set -s "$subscrId"
az account show

echo "Setting up Container App: $appName"
echo "Resource Group: $resGroup"
echo "Location: $location"
echo "ACR: $acrName"

echo "Checking if resource group exists..."
if [[ $(az group list --query "[?name=='$resGroup']" -o tsv) = "" ]]; then
    echo "Creating resource group: $resGroup"
    az group create --name "$resGroup" --location "$location"
    echo "Resource group created"
else
    echo "Resource group already exists"
fi

echo "Checking if ACR exists..."
if [[ $(az acr list --query "[?name=='$acrName']" -o tsv) = "" ]]; then
    echo "Creating Azure Container Registry: $acrName"
    az acr create \
        --resource-group "$resGroup" \
        --name "$acrName" \
        --sku Basic \
        --admin-enabled true
    echo "ACR created"
else
    echo "ACR already exists"
fi

echo "Building and pushing Docker image to ACR..."
az acr build \
    --registry "$acrName" \
    --image "$imageName:latest" \
    --file "$CURRENT_FOLDER/../Dockerfile" \
    "$CURRENT_FOLDER/.."

echo "Retrieving ACR credentials..."
acrLoginServer=$(az acr show --name "$acrName" --query loginServer -o tsv)
acrPassword=$(az acr credential show --name "$acrName" --query "passwords[0].value" -o tsv)

echo "Checking if Container Apps environment exists..."
if [[ $(az containerapp env list -g "$resGroup" --query "[?name=='$containerAppsEnv']" -o tsv) = "" ]]; then
    echo "Creating Container Apps environment: $containerAppsEnv"
    az containerapp env create \
        --name "$containerAppsEnv" \
        --resource-group "$resGroup" \
        --location "$location"
    echo "Container Apps environment created"
else
    echo "Container Apps environment already exists"
fi

echo "Deploying Container App: $appName"
if [[ $(az containerapp list -g "$resGroup" --query "[?name=='$appName']" -o tsv) = "" ]]; then
    echo "Creating Container App..."
    az containerapp create \
        --name "$appName" \
        --resource-group "$resGroup" \
        --environment "$containerAppsEnv" \
        --image "$acrLoginServer/$imageName:latest" \
        --registry-server "$acrLoginServer" \
        --registry-username "$acrName" \
        --registry-password "$acrPassword" \
        --target-port 5000 \
        --ingress external \
        --cpu "$cpu" \
        --memory "$memory" \
        --min-replicas 0 \
        --max-replicas 3 \
        --env-vars \
            AZURE_TENANT_ID="$tenantId" \
            AZURE_CLIENT_ID="$clientId" \
            AZURE_CLIENT_SECRET=secretref:client-secret \
            KUSTO_CLUSTER_URL="$kustoClusterUrl" \
            KUSTO_DATABASE_NAME="$kustoDatabaseName" \
            MCP_BASE_URL=https://placeholder.azurecontainerapps.io \
        --secrets client-secret="<replace-with-actual-secret>"
    echo "Container App created"
else
    echo "Container App already exists, updating..."
    az containerapp update \
        --name "$appName" \
        --resource-group "$resGroup" \
        --image "$acrLoginServer/$imageName:latest" \
        --set-env-vars \
            AZURE_TENANT_ID="$tenantId" \
            AZURE_CLIENT_ID="$clientId" \
            KUSTO_CLUSTER_URL="$kustoClusterUrl" \
            KUSTO_DATABASE_NAME="$kustoDatabaseName"
    echo "Container App updated"
fi

echo "Retrieving Container App URL..."
appUrl=$(az containerapp show \
    --name "$appName" \
    --resource-group "$resGroup" \
    --query properties.configuration.ingress.fqdn -o tsv)

echo ""
echo "Deployment completed"
echo ""
echo "Container App URL: https://$appUrl"
echo "  Health: https://$appUrl/health"
echo "  MCP:    https://$appUrl/mcp"
echo ""
echo "Post-deployment steps:"
echo "  1. Set the client secret:"
echo "     az containerapp secret set --name $appName -g $resGroup --secrets client-secret=<your-secret>"
echo ""
echo "  2. Update MCP_BASE_URL to the actual URL:"
echo "     az containerapp update --name $appName -g $resGroup --set-env-vars MCP_BASE_URL=https://$appUrl"
echo ""
echo "  3. Add the redirect URI to the Entra ID app registration:"
echo "     The redirect URI will depend on your MCP client (e.g. Claude Desktop)"
echo ""
echo "  4. Grant Kusto access if using managed identity:"
echo "     .add database $kustoDatabaseName viewers ('aadapp=<managed-identity-client-id>')"
