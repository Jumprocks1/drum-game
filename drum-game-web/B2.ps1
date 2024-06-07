param(
    [Parameter(Mandatory)][string]$BjsonName
)
$ErrorActionPreference = "Stop"

# Note, this does work for uploading updates to maps, just make sure to adjust dtx.json as necessary.
# It adds a second entry at the top for the new file.

$authUrl = "https://api.backblazeb2.com/b2api/v3/b2_authorize_account"
$key = Get-Content backblaze-b2.key

if (!$BjsonName.EndsWith(".bjson")) {
    $BjsonName += ".bjson"
}

$bjsonNameLeaf = Split-Path $BjsonName -LeafBase
$exportFolder = "../drum-game-private/resources/dtx-exports/$bjsonNameLeaf-dtx"
$zipTarget = (ls $exportFolder *.zip | sort LastWriteTime | select -last 1).FullName
if (!(Test-Path $zipTarget)) {
    echo "$zipTarget not found"
    exit
}

$encodedCreds = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($key))

$basicAuthValue = "Basic $encodedCreds"

$headers = @{Authorization = $basicAuthValue}

$auth = irm -Uri $authUrl -Headers $headers
$apiUrl = $auth.apiInfo.storageApi.apiUrl


$headers.Authorization = $auth.authorizationToken

$bucketName = $auth.apiInfo.storageApi.bucketName
$body = @{bucketId = $auth.apiInfo.storageApi.bucketId}

$url = "$apiUrl/b2api/v3/b2_get_upload_url"
$res = irm -Uri $url -Headers $headers -Body $body -SkipHeaderValidation
$uploadUrl = $res.uploadUrl
$uploadAuthToken = $res.authorizationToken

$sha1 = (Get-FileHash -Algorithm SHA1 $zipTarget).Hash

$headers.Authorization = $uploadAuthToken
$filenameLeaf = Split-Path $zipTarget -Leaf
$headers["X-Bz-File-Name"] = [uri]::EscapeUriString($filenameLeaf)
$headers["Content-Type"] = "b2/x-auto"
$headers["X-Bz-Content-Sha1"] = $sha1

$uploadRes = irm -Uri $uploadUrl -Headers $headers -SkipHeaderValidation -Method Post -InFile $zipTarget
$uploadedFilename = [uri]::EscapeUriString($uploadRes.fileName)

$downloadUrlBase = $auth.apiInfo.storageApi.downloadUrl
$downloadUrl = "$downloadUrlBase/file/$bucketName/$uploadedFilename"

$dtxJsonPath = "src/dtx.json"
$dtxJson = Get-Content -Raw $dtxJsonPath | ConvertFrom-Json
$dtxJson.maps = @(@{
    filename = $BjsonName;
    downloadUrl = $downloadUrl;
    date = get-date -Format "yyyy-M-d";
}) + $dtxJson.maps
$dtxJson | ConvertTo-Json -depth 10 | Out-File $dtxJsonPath -NoNewLine

# force git to recheck the file
git rm --cached ../resources/maps/$BjsonName
git add $dtxJsonPath ../resources/maps/$BjsonName
