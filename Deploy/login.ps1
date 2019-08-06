$subscriptionName = "MVP demos"

while ($true) {
    Write-Host ("Azure RM PowerShell - Switching to subscription " + $subscriptionName)
    $sub = Select-AzSubscription -SubscriptionName $subscriptionName -ErrorAction SilentlyContinue
    if ($sub) { break }
    Write-Host "Subcription not found with current credentials"
    Connect-AzAccount | Out-Null
}

while ($true) {
    Write-Host ("Azure CLI - Switching to subscription " + $subscriptionName)
    az account set --subscription $subscriptionName
    if ($?) { break; }
    Write-Host "Subcription not found with current credentials"
    az login
}
