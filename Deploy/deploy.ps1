$resourceGroupName = "mxa-keda"
$locationName = "northeurope"

$k8sClusterName = "mxakedak8s"
$k8sNamespace = "mxa-keda"
$vnetName = "mxa-keda-vnet"
$aksSubnetName = "aks-subnet"
$virtualNodesSubnetName = "virtualnodes-subnet"

$sbNamespaceName = "mxakedasb"
$workQueueName = "kedawork"
$outputQueueName = "kedaoutput"

$storageName = "mxakedastorage"
$webContainerName = "web"

$acrServer = "mxacommonacr.azurecr.io"

function ConvertTo-Object {
    ConvertFrom-Json ([String]::Join(" ", $args[0]))
}

az group delete --name $resourceGroupName --yes
az group create --name $resourceGroupName --location $locationName

$json = az network vnet create `
    --name $vnetName `
    --resource-group $resourceGroupName `
    --location $locationName `
    --address-prefixes 10.0.0.0/8 `
    --subnet-name $aksSubnetName `
    --subnet-prefix 10.240.0.0/16
$vnet = (ConvertTo-Object $json).newVNet

$json = az network vnet subnet show `
    --resource-group $resourceGroupName `
    --vnet-name $vnetName `
    --name $aksSubnetName
$aksSubnet = ConvertTo-Object $json

az network vnet subnet create `
    --name $virtualNodesSubnetName `
    --vnet-name $vnetName `
    --resource-group $resourceGroupName `
    --address-prefixes 10.241.0.0/16

$json = az ad sp create-for-rbac --skip-assignment
$sp = ConvertTo-Object $json

do {
    az role assignment create --assignee $sp.appId --scope $vnet.id --role Contributor
} while (!($?))

az aks create `
    --name $k8sClusterName `
    --resource-group $resourceGroupName `
    --location $locationName `
    --node-count 1 `
    --network-plugin azure `
    --service-cidr 10.0.0.0/16 `
    --dns-service-ip 10.0.0.10 `
    --docker-bridge-address 172.17.0.1/16 `
    --vnet-subnet-id $aksSubnet.id `
    --service-principal $sp.appId `
    --client-secret $sp.password

az aks enable-addons `
    --name $k8sClusterName `
    --resource-group $resourceGroupName `
    --addons virtual-node `
    --subnet-name $virtualNodesSubnetName

az aks get-credentials --resource-group $resourceGroupName --name $k8sClusterName --overwrite-existing

az servicebus namespace create `
    --name $sbNamespaceName `
    --resource-group $resourceGroupName `
    --location $locationName `
    --sku Standard
az servicebus queue create `
    --name $workQueueName `
    --namespace-name $sbNamespaceName `
    --resource-group $resourceGroupName
az servicebus queue create `
    --name $outputQueueName `
    --namespace-name $sbNamespaceName `
    --resource-group $resourceGroupName

az storage account create `
    --name $storageName `
    --resource-group $resourceGroupName `
    --location $locationName `
    --sku Standard_LRS
az storage container create `
    --name $webContainerName `
    --account-name $storageName `
    --public-access container
az storage blob upload `
    --file .\index.html `
    --name index.html `
    --container-name $webContainerName `
    --account-name $storageName
az storage blob upload `
    --file .\status.json `
    --name status.json `
    --container-name $webContainerName `
    --account-name $storageName

$json = az servicebus namespace authorization-rule list `
    --namespace-name $sbNamespaceName `
    --resource-group $resourceGroupName
$sbAuthRuleName = (ConvertTo-Object $json)[0].name

$json = az servicebus namespace authorization-rule keys list `
    --name $sbAuthRuleName `
    --namespace-name $sbNamespaceName `
    --resource-group $resourceGroupName
$sbConnStr = (ConvertTo-Object $json).primaryConnectionString

$json = az acr credential show --name mxacommonacr
$acrUsername = (ConvertTo-Object $json).username
$acrPassword = (ConvertTo-Object $json).passwords[0].value

$json = az storage account show-connection-string --name $storageName
$storageConnStr = (ConvertTo-Object $json).connectionString

kubectl create secret generic keda-secrets `
    --from-literal=sbConnectionString=$sbConnStr `
    --from-literal=storageConnectionString=$storageConnStr

kubectl create secret docker-registry acr-auth `
    --docker-server $acrServer `
    --docker-username $acrUsername `
    --docker-password $acrPassword `
    --docker-email markxa@markxa.com

kubectl apply -f kedaWorker.yaml
kubectl apply -f queueLoader.yaml
kubectl apply -f service.yaml

kubectl get services --output wide --watch
