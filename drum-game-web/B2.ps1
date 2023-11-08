param(
    [Parameter(Mandatory)][string]$target
)
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
$body = @{
    accountId = $auth.accountId;
    bucketId = $auth.apiInfo.storageApi.bucketId
}

$url = "$apiUrl/b2api/v3/b2_get_upload_url"
$res = irm -Uri $url -Headers $headers -Body $body -SkipHeaderValidation
$uploadUrl = $res.uploadUrl
$uploadAuthToken = $res.authorizationToken

$sha1 = (Get-FileHash -Algorithm SHA1 $target).Hash

$headers.Authorization = $uploadAuthToken
$filenameLeaf = Split-Path $target -Leaf
$headers["X-Bz-File-Name"] = [uri]::EscapeUriString($filenameLeaf)
$headers["Content-Type"] = "b2/x-auto"
$headers["X-Bz-Content-Sha1"] = $sha1

$uploadRes = irm -Uri $uploadUrl -Headers $headers -SkipHeaderValidation -Method Post -InFile $target
$uploadedFilename = [uri]::EscapeUriString($uploadRes.fileName)

$downloadUrlBase = $auth.apiInfo.storageApi.downloadUrl
$downloadUrl = "$downloadUrlBase/file/$bucketName/$uploadedFilename"

$dtxJsonPath = "src/dtx.json"
$dtxJson = Get-Content -Raw $dtxJsonPath | ConvertFrom-Json
$dtxJson.maps = @(@{
    filename = $targetBjson;
    downloadUrl = $downloadUrl;
    date = get-date -Format "yyyy-M-d";
}) + $dtxJson.maps
$dtxJson | ConvertTo-Json -depth 10 | Out-File $dtxJsonPath -NoNewLine
