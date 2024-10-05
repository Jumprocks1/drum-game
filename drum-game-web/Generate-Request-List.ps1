$ErrorActionPreference = "Stop"
$par = New-Object System.Collections.Generic.List[string]

$par.Add("-c")
$par.Add("ClearSearch")
$par.Add("-c")
$par.Add("SelectCollection{Stream TODO}")
$par.Add("-c")
$par.Add("ExportSearchToFile{RequestListJson,true}")
$par.Add("-c")
$par.Add("QuitGame")

& "..\drum-game-private\DrumGame.Desktop\bin\Debug\net8.0\DrumGame.exe" @par

$newFile = ls "..\drum-game-private\resources\exports" | sort LastWriteTime | select -Last 1

if ($newFile.LastWriteTime -lt (Get-Date).AddMinutes(-5)) {
    throw "New file not found"
}

mv $newFile "request-list.json"

$uploadTarget = "request-list.json"


# Note, this does work for uploading updates to maps, just make sure to adjust dtx.json as necessary.
# It adds a second entry at the top for the new file.

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
$url = "$apiUrl/b2api/v3/b2_get_upload_url"
$res = irm -Uri $url -Headers $headers -Body $body -SkipHeaderValidation
$uploadUrl = $res.uploadUrl
$uploadAuthToken = $res.authorizationToken

$sha1 = (Get-FileHash -Algorithm SHA1 $uploadTarget).Hash

$headers.Authorization = $uploadAuthToken
$filenameLeaf = Split-Path $uploadTarget -Leaf
$headers["X-Bz-File-Name"] = [uri]::EscapeUriString($filenameLeaf)
$headers["Content-Type"] = "b2/x-auto"
$headers["X-Bz-Content-Sha1"] = $sha1

$uploadRes = irm -Uri $uploadUrl -Headers $headers -SkipHeaderValidation -Method Post -InFile $uploadTarget
$uploadedFilename = [uri]::EscapeUriString($uploadRes.fileName)

$downloadUrlBase = $auth.apiInfo.storageApi.downloadUrl
$downloadUrl = "$downloadUrlBase/file/$bucketName/$uploadedFilename"

echo "Uploaded to $downloadUrl"

rm $uploadTarget
