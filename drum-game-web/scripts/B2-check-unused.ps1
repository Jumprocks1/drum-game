$ErrorActionPreference = "Stop"

$authUrl = "https://api.backblazeb2.com/b2api/v3/b2_authorize_account"
$key = Get-Content backblaze-b2.key
$encodedCreds = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($key))
$basicAuthValue = "Basic $encodedCreds"
$headers = @{Authorization = $basicAuthValue}
$auth = irm -Uri $authUrl -Headers $headers
$apiUrl = $auth.apiInfo.storageApi.apiUrl
$headers.Authorization = $auth.authorizationToken

$bucketName = $auth.apiInfo.storageApi.bucketName
$body = @{bucketId = $auth.apiInfo.storageApi.bucketId}

$url = "$apiUrl/b2api/v3/b2_list_file_names"
$res = irm -Uri $url -Headers $headers -Body $body -SkipHeaderValidation
$files = $res.files

$global:res = $files

$dtxJsonPath = "src/dtx.json"
$dtxJson = Get-Content -Raw $dtxJsonPath | ConvertFrom-Json

$prefix = "https://f005.backblazeb2.com/file/DrumGameDTX/"
$set = @{}
foreach ($map in $dtxJson.maps) {
    $set.Add($map.downloadUrl.split($prefix)[1], $true)
}

foreach ($file in $files) {
    $uploadedFilename = [uri]::EscapeUriString($file.fileName)
    if (!$set.Contains($uploadedFilename)){
        echo $file.fileName
    }
}
